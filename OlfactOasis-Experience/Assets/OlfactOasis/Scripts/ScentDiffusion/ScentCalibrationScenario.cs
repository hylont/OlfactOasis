using EditorAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Trimmed down to a single odor for the demo : one booth, one ScentData, no more walking between
// stands. Started explicitly via Begin() (wired from TutorialScenario.OnTutorialComplete) rather
// than on Start(), since it now runs after the tutorial instead of first thing in the scene.
public class ScentCalibrationScenario : MonoBehaviour, IButtonListener, IPlayerGesturesListener
{
    enum EScenarioStep
    {
        WaitingForApproach,
        Printing,
        WaitingForButtonPress,
        Diffusing,
        WaitingPerceivedAnswer,
        WaitingPleasantAnswer,
        Finished
    }

    [Serializable]
    class ScentCalibrationResults
    {
        public List<ScentData> ScentsData;
    }

    [Header("Protocol config")]
    //1x3, 3x3, 7x3, 20x3, 55x3, 100x3
    public List<int> ScentStrengthsConfigs = new() { 1, 3, 7, 20, 55, 100};
    public int ScentDuration = 3000;
    public float PerceptionQuestionDelay = 10f;
    public float ApproachDistance = 1.5f;

    public int NegativeAnswersUntilSkip = 3;

    [Header("Booth containing the push button and the printed vial")]
    [SerializeField] OlfactiveCalibrationBooth _booth;

    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;
    [SerializeField] Player _player;
    [SerializeField] OlfyHandler _scentDiffuser;

    [Header("Next scene")]
    [SerializeField] SmoothSceneSwitch _sceneSwitch;
    [SerializeField] string _storeSceneName = "StoreScene";

    [Header("Debug")]
    [SerializeField] bool _verbose = false;

    [Header("Python data reader")]
    [SerializeField] string _pythonExecutable = "python";
    [SerializeField] string _dataReaderScriptRelativePath = "../../OlfactoasisDataReader.py";
    [SerializeField] bool _openDataAtEachEvaluation = true;
    [SerializeField] bool _openDataAtCalibrationEnd = true;

    [ShowInInspector] EScenarioStep _currentStep;
    int _currentStrengthIndex;
    int _consecutiveNegativeAnswers;
    bool _hasApproachedBooth;
    bool _hasBegun;

    ScentDiffusionParameters _currentParameters;
    EUserResponse _wasPerceived;
    EUserResponse _wasPleasant;

    public void Begin()
    {
        if (!_clipsReceiverGameObject.TryGetComponent(out _clipsReceiver))
        {
            LLogger.E("ScentCalibrationScenario: ClipsReceiver does not have a valid IClipsReceiver component.");
        }

        if (_clipsReceiver == null || _player == null || _player.Head == null || _scentDiffuser == null || _booth == null)
        {
            LLogger.E("ScentCalibrationScenario: missing a required dependency (Argos, Player, Player.Head, ScentDiffuser or booth).");
            return;
        }

        _player.AddListener(this);
        _hasBegun = true;

        StartIntroduction();
    }

    void OnDestroy()
    {
        if (_booth != null && _booth.PushButton != null) _booth.PushButton.RemoveListener(this);
        if (_player != null) _player.RemoveListener(this);
    }

    void Update()
    {
        if (!_hasBegun || _currentStep != EScenarioStep.WaitingForApproach) return;

        bool isNearby = Vector3.Distance(_player.Head.transform.position, _booth.transform.position) <= ApproachDistance;
        if (isNearby == _hasApproachedBooth) return;

        _hasApproachedBooth = isNearby;
        if (isNearby) OnBoothApproached();
    }

    void SetStep(EScenarioStep newStep)
    {
        if (_verbose) LLogger.LogOnScreenOnly($"ScentCalibrationScenario: {_currentStep} => {newStep}.\n" +
            $"({_booth.name}) : {_booth.ScentData.Name}");
        _currentStep = newStep;
    }

    // Step 1: Argos looks at the booth, it appears, and the intro line plays.
    void StartIntroduction()
    {
        _hasApproachedBooth = false;

        //_argos.LookAt(_booth.transform);

        _booth.PushButton.AddListener(this);
        _booth.Appear();

        _clipsReceiver.HandleClip("argos.calibration.ordreatelier", null);

        SetStep(EScenarioStep.WaitingForApproach);
    }

    // Step 2: the player approached the booth
    void OnBoothApproached()
    {
        StartPrinting();
    }

    // Step 3: the vial for the upcoming strength gets printed - its collider (and the push button)
    // only become usable once printing completes.
    void StartPrinting()
    {
        SetStep(EScenarioStep.Printing);

        _booth.PlayPrinterAnimation(() =>
        {
            _booth.ActivateVialCollider();
            _clipsReceiver.HandleClip("argos.calibration.ordrebouton", null);
            SetStep(EScenarioStep.WaitingForButtonPress);
        });
    }

    public void OnButtonDown()
    {
        if (_currentStep != EScenarioStep.WaitingForButtonPress) return;

        DiffuseCurrentStrength();
    }

    public void OnButtonUp()
    {
    }

    // Step 4: diffuse the current strength config for this booth's scent.
    void DiffuseCurrentStrength()
    {
        ScentData currentScentData = _booth.ScentData;

        float strength = ScentStrengthsConfigs[_currentStrengthIndex];
        _currentParameters = new ScentDiffusionParameters(currentScentData.SlotIndex, strength, ScentDuration, currentScentData.DefaultVibrationFrequency);

        _wasPerceived = EUserResponse.NeutralUndecided;
        _wasPleasant = EUserResponse.NeutralUndecided;

        if (_booth.DiffusionVFX != null) _booth.DiffusionVFX.Play();
        _scentDiffuser.RequestDiffusion(_currentParameters);

        SetStep(EScenarioStep.Diffusing);
        StartCoroutine(WaitForPerceptionQuestionRoutine());
    }

    // Step 5: ask whether the scent was perceived, once it's had time to reach the player.
    IEnumerator WaitForPerceptionQuestionRoutine()
    {
        yield return new WaitForSeconds(PerceptionQuestionDelay);

        _clipsReceiver.HandleClip("argos.calibration.questionperception", _booth.TextSpawnAnchor);
        SetStep(EScenarioStep.WaitingPerceivedAnswer);
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = default)
    {
        switch (_currentStep)
        {
            case EScenarioStep.WaitingPerceivedAnswer:
                HandlePerceivedAnswer(gesture);
                break;

            case EScenarioStep.WaitingPleasantAnswer:
                HandlePleasantAnswer(gesture);
                break;
        }
    }

    // Step 6: thumb down or a flat hand means nothing more to ask, thumb up moves on to step 7.
    void HandlePerceivedAnswer(EPlayerGesture gesture)
    {
        switch (gesture)
        {
            case EPlayerGesture.ThumbDown:
                _wasPerceived = EUserResponse.Negative;
                CompleteTrial();
                break;

            case EPlayerGesture.HorizontalHand:
                _wasPerceived = EUserResponse.NeutralUndecided;
                CompleteTrial();
                break;

            case EPlayerGesture.ThumbUp:
                _wasPerceived = EUserResponse.Positive;
                _clipsReceiver.HandleClip("argos.calibration.questionagreable", _booth.TextSpawnAnchor);
                SetStep(EScenarioStep.WaitingPleasantAnswer);
                break;
        }
    }

    // Step 8: a flat hand skips straight to storing the trial, thumb up/down react and move on to the curve.
    void HandlePleasantAnswer(EPlayerGesture gesture)
    {
        switch (gesture)
        {
            case EPlayerGesture.HorizontalHand:
                _wasPleasant = EUserResponse.NeutralUndecided;
                CompleteTrial();
                break;

            case EPlayerGesture.ThumbUp:
                _wasPleasant = EUserResponse.Positive;
                _clipsReceiver.HandleClip("argos.calibration.reponseagreable", _booth.TextSpawnAnchor);
                RecordResponseCurve();
                break;

            case EPlayerGesture.ThumbDown:
                _wasPleasant = EUserResponse.Negative;
                _clipsReceiver.HandleClip("argos.calibration.reponsedesagreable", _booth.TextSpawnAnchor);
                RecordResponseCurve();
                break;
        }
    }

    // Steps 9-10: prompt the intensity curve, then record it.
    void RecordResponseCurve()
    {
        _booth.CurveDrawingMethod.StartDraw(() => CompleteTrial());
    }

    // Step 11: store the trial, then either move to the next strength or end the calibration.
    void CompleteTrial()
    {
        List<Vector3> curvePoints = _booth.CurveDrawingMethod.GetPoints();
        ScentEvaluation evaluation = new(_currentParameters, _wasPerceived, _wasPleasant, curvePoints ?? new List<Vector3>());
        _booth.ScentData.Evaluations.Add(evaluation);

        _consecutiveNegativeAnswers = _wasPleasant == EUserResponse.Negative ? _consecutiveNegativeAnswers + 1 : 0;

        _currentStrengthIndex++;

        bool allStrengthsDone = _currentStrengthIndex >= ScentStrengthsConfigs.Count;
        bool tooManyNegativeAnswers = _consecutiveNegativeAnswers >= NegativeAnswersUntilSkip;

        if (!allStrengthsDone && !tooManyNegativeAnswers)
        {
            StartPrinting();
            return;
        }

        EndScenario();
    }

    // The odor has just been fully evaluated (every strength tried, or skipped) - persist it,
    // keep the optimal (most pleasant) intensity, and move on to the store.
    void EndScenario()
    {
        SetStep(EScenarioStep.Finished);
        //_argos.StopLookAt();

        _booth.Disappear();

        ExportCurrentBoothEvaluations();
        ComputeOptimalIntensity();
        SaveResultsToJson();

        if (_sceneSwitch != null) _sceneSwitch.SwitchScene(_storeSceneName);
    }

    // Keeps, as the optimal intensity, the strength that was rated pleasant (thumb up) with the
    // longest "how pleasant" trace - i.e. the maximum agreeability recorded during calibration.
    void ComputeOptimalIntensity()
    {
        ScentEvaluation best = _booth.ScentData.Evaluations
            .Where(evaluation => evaluation.WasPleasant == EUserResponse.Positive)
            .OrderByDescending(evaluation => evaluation.ResponseMagnitude)
            .FirstOrDefault();

        if (best == null)
        {
            LLogger.W("ScentCalibrationScenario: no pleasant evaluation recorded, falling back to the last tried strength.");
            best = _booth.ScentData.Evaluations.LastOrDefault();
        }

        if (best == null)
        {
            LLogger.E("ScentCalibrationScenario: no evaluation recorded at all, can't determine an optimal intensity.");
            return;
        }

        _booth.ScentData.OptimalParameters = best.Parameters;

        CalibrationResultHolder.ChosenScent = _booth.ScentData.Name;
        CalibrationResultHolder.OptimalParameters = best.Parameters;
    }

    void ExportCurrentBoothEvaluations()
    {
        string directory = Path.Combine(Application.persistentDataPath, "ScentCalibrationResults");
        string csvPath = ScentEvaluationCsvExporter.Save(_booth.ScentData, directory);

        if (csvPath != null && _openDataAtEachEvaluation)
        {
            ScentEvaluationCsvExporter.LaunchDataReader(_pythonExecutable, _dataReaderScriptRelativePath, csvPath, _booth.ScentData.Name);
        }
    }

    void SaveResultsToJson()
    {
        ScentCalibrationResults results = new() { ScentsData = new List<ScentData> { _booth.ScentData } };
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
