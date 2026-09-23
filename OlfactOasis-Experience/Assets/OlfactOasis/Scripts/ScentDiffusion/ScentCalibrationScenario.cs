using EditorAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class ScentCalibrationScenario : MonoBehaviour
{
    [Serializable]
    class ScentCalibrationResults
    {
        public List<ScentData> ScentsData;
    }

    [Header("Protocol config")]

    [SerializeField] List<ScentCalibrationBooth> _booths = new();
    [SerializeField] int MinValidBoothsNeeded = 1;
    int _boothIndex = -1;

    //1x3, 3x3, 7x3, 20x3, 55x3, 100x3
    public List<int> ScentStrengthsConfigs = new() { 1, 3, 7, 20, 55, 100};
    public int ScentDuration = 3000;
    public float PerceptionQuestionDelay = 10f;

    public int NegativeAnswersUntilSkip = 2;
    public float MinDistanceFromHeadToDiffuse = .15f;

    [ShowInInspector] int successfulCalibrations = 0;


    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;
    public Player Player;
    public OlfyHandler ScentDiffuser;

    [Header("On Calibration ended")]
    public UnityEvent OnCalibrationEnded;

    [Header("Orders and lines")]
    [TextArea, SerializeField] string _introInstruction;
    [TextArea, SerializeField] string _calibrationSuccessfulInstruction;
    [TextArea, SerializeField] string _noOtherScentInstruction;
    [Header("Debug")]
    [SerializeField] bool _verbose = false;

    [Header("Python data reader")]
    [SerializeField] string _pythonExecutable = "python";
    [SerializeField] string _dataReaderScriptRelativePath = "../../OlfactoasisDataReader.py";
    [SerializeField] bool _openDataAtEachBooth = true;
    [SerializeField] bool _openDataAtCalibrationEnd = true;


    // Step 1: Argos looks at the booth, it appears, and the intro line plays.

    private void Start()
    {
        _clipsReceiver = _clipsReceiverGameObject.GetComponent<IClipsReceiver>();

        foreach(ScentCalibrationBooth booth in _booths)
        {
            booth.gameObject.SetActive(false);
        }
    }

    public void StartIntroduction()
    {
        _clipsReceiver.HandleClip(_introInstruction);

        foreach(ScentCalibrationBooth booth in _booths)
        {
            booth.SetState(EScentCalibrationStep.WAITING);
        }

        if(_booths.Count == 0)
        {
            LLogger.E("No booths given");
            return;
        }

        NextBooth();
    }

    public void OnBoothEnded()
    {
        bool successfulOptimal = ComputeOptimalIntensity();
        SaveResultsToJson(_booths[_boothIndex]);
                
        if (_openDataAtEachBooth)
        {
            ExportCurrentBoothEvaluations(_booths[_boothIndex]);
        }

        if (successfulOptimal) successfulCalibrations++;

        NextBooth();
    }

    void NextBooth()
    {
        if(_booths.Count == 0)
        {
            LLogger.E("No booths, aborting");
            return;
        }

        if (_boothIndex >= 0)
        {
            //disable previous booth
            _booths[_boothIndex].gameObject.SetActive(false);
        }

        _boothIndex++;
        
        if (successfulCalibrations < MinValidBoothsNeeded)
        {
            if (_boothIndex < _booths.Count)
            {                
                //go to next booth, basic shit
                _booths[_boothIndex].gameObject.SetActive(true);
                _booths[_boothIndex].SetState(EScentCalibrationStep.READY);
            }
            else
            {
                _clipsReceiver.HandleClip(_noOtherScentInstruction);
            }
        }
        else
        {
            _clipsReceiver.HandleClip(_calibrationSuccessfulInstruction);
            OnCalibrationEnded.Invoke();
        }
    }

    // Keeps, as the optimal intensity, the strength that was rated pleasant (thumb up) with the
    // longest "how pleasant" trace - i.e. the maximum agreeability recorded during calibration.
    bool ComputeOptimalIntensity()
    {
        ScentEvaluation best = _booths[_boothIndex].Evaluations
            .Where(evaluation => evaluation.WasPleasant == EUserResponse.Positive)
            .OrderByDescending(evaluation => evaluation.ResponseMagnitude)
            .FirstOrDefault();

        if (best == null)
        {
            LLogger.W("ScentCalibrationScenario: no pleasant evaluation recorded, falling back to the last tried strength.");
            return false;
        }

        if (best == null)
        {
            LLogger.E("ScentCalibrationScenario: no evaluation recorded at all, can't determine an optimal intensity.");
            return false;
        }

        _booths[_boothIndex].ScentData.OptimalParameters = best.Parameters;

        return true;
    }

    void ExportCurrentBoothEvaluations(ScentCalibrationBooth booth)
    {
        string directory = Path.Combine(Application.persistentDataPath, "ScentCalibrationResults");
        string csvPath = ScentEvaluationCsvExporter.Save(booth.ScentData, directory);

        if (csvPath != null && _openDataAtEachBooth)
        {
            ScentEvaluationCsvExporter.LaunchDataReader(_pythonExecutable, _dataReaderScriptRelativePath, csvPath, booth.ScentData.Name);
        }
    }

    void SaveResultsToJson(ScentCalibrationBooth booth)
    {
        ScentCalibrationResults results = new() { ScentsData = new List<ScentData> { booth.ScentData } };
        string json = JsonUtility.ToJson(results, true);

        string directory = Path.Combine(Application.persistentDataPath, "ScentCalibrationResults");
        string path = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd_HHmmss}.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json);
            LLogger.L($"ScentCalibrationScenario: results saved to {path}");
        }
        catch (Exception e)
        {
            LLogger.E($"ScentCalibrationScenario: failed to save results - {e}");
        }
    }
}
