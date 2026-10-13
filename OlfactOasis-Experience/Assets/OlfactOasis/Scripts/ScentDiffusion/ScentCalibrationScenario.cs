using EditorAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class ScentCalibrationScenario : MonoBehaviour
{
    [Header("Protocol config")]

    [SerializeField] List<ScentCalibrationBooth> _booths = new();
    [SerializeField] int MinValidBoothsNeeded = 1;
    [Tooltip("Presents the booths in a random order, so that no scent is favoured by always being tried first.")]
    [SerializeField] bool _randomizeBoothsOrder = true;
    int _boothIndex = -1;

    //1x3, 3x3, 7x3, 20x3, 55x3, 100x3
    public List<int> ScentStrengthsConfigs = new() { 1, 3, 7, 20, 55, 100};
    public int ScentDuration = 3000;
    public float PerceptionQuestionDelay = 10f;

    public int NegativeAnswersUntilSkip = 2;
    public float MinDistanceFromHeadToDiffuse = .15f;

    [ShowInInspector] int successfulCalibrations = 0;
    ScentCalibrationResults _results;

    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    public IClipsReceiver ClipsReceiver;
    public Player Player;
    public OlfyHandler ScentDiffuser;

    [Header("On Calibration ended (set fader for delay)")]
    [SerializeField] ScreenFader _fader;
    public UnityEvent OnCalibrationEnded;

    [Header("Orders and lines")]
    [TextArea, SerializeField] string _introInstruction;
    [TextArea, SerializeField] string _calibrationSuccessfulInstruction;
    [TextArea, SerializeField] string _noOtherScentInstruction;
    [Header("Debug")]
    [SerializeField] bool _verbose = false;

    // Step 1: Argos looks at the booth, it appears, and the intro line plays.

    private void Awake()
    {
        ClipsReceiver = _clipsReceiverGameObject.GetComponent<IClipsReceiver>();

        foreach(ScentCalibrationBooth booth in _booths)
        {
            booth.gameObject.SetActive(false);
        }
    }

    public void StartIntroduction()
    {
        ClipsReceiver.HandleClip(_introInstruction);

        foreach (ScentCalibrationBooth booth in _booths)
        {
            booth.Init();
        }

        if (_booths.Count == 0)
        {
            LLogger.E("No booths given");
            return;
        }

        if (_randomizeBoothsOrder) ShuffleBooths();

        _results = new ScentCalibrationResults(ParticipantData.GetID());
        NextBooth();
    }

    // Fisher-Yates shuffle
    void ShuffleBooths()
    {
        for (int i = _booths.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_booths[i], _booths[j]) = (_booths[j], _booths[i]);
        }

        if (_verbose) LLogger.L($"Booths order: {string.Join(", ", _booths.Select(booth => booth.ScentData.Name))}");
    }

    public void OnBoothEnded()
    {
        bool successfulOptimal = ComputeOptimalIntensity();
        _results.AddScent(_booths[_boothIndex].ScentData);
        _results.Save();

        if (successfulOptimal)
        {
            successfulCalibrations++;
        }

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
                ClipsReceiver.HandleClip(_noOtherScentInstruction);
            }
        }
        else
        {
            ClipsReceiver.HandleClip(_calibrationSuccessfulInstruction);
            if (_fader)
            {
                _fader.FadeInAndOut();
                StartCoroutine(EndScenario_Delayed(_fader.FadeDuration));
            }
            else
            {
                OnCalibrationEnded.Invoke();
            }
        }
    }

    private IEnumerator EndScenario_Delayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        OnCalibrationEnded.Invoke();
    }

    // Keeps, as the optimal intensity, the strength that was rated pleasant (thumb up) with the
    // longest "how pleasant" trace - i.e. the maximum agreeability recorded during calibration.
    bool ComputeOptimalIntensity()
    {
        ScentEvaluation best = _booths[_boothIndex].ScentData.Evaluations
            .Where(evaluation => evaluation.WasPleasant == EUserResponse.Positive)
            .OrderByDescending(evaluation => evaluation.ResponseMagnitude)
            .FirstOrDefault();

        if (best == null)
        {
            LLogger.W("ScentCalibrationScenario: no pleasant evaluation recorded, can't determine an optimal intensity.");
            return false;
        }

        _booths[_boothIndex].ScentData.OptimalParameters = best.Parameters;

        return true;
    }

    internal List<ScentData> GetAllOptimals()
    {
        if(_booths.Count == 0 || successfulCalibrations == 0)
        {
            LLogger.L("No booth or not any optimal scent");
            return null;
        }

        List<ScentData> optimums = new();
        foreach(ScentCalibrationBooth booth in _booths)
        {
            if(booth.ScentData.OptimalParameters != null && booth.ScentData.OptimalParameters.Strength != 0)
            {
                optimums.Add(booth.ScentData);
            }
        }
        return optimums;
    }
}
