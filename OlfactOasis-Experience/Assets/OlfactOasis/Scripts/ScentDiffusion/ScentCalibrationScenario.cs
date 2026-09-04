using EditorAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ScentCalibrationScenario : MonoBehaviour, IButtonListener, IPlayerGesturesListener
{
    enum EScenarioStep
    {
        WaitingForApproach,
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
    public List<float> ScentStrengthsConfigs = new() { .01f, .03f, .07f, .2f, .55f, 1f };
    public float ScentDuration = 3f;
    public float PerceptionQuestionDelay = 10f;
    public float ApproachDistance = 1.5f;

    public int NegativeAnswersUntilSkip = 3;

    [Header("Booths containing a push button")]
    [SerializeField] List<OlfactiveCalibrationBooth> _olfactiveBooths = new();

    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;
    [SerializeField] Player _player;
    [SerializeField] OlfyHandler _scentDiffuser;

    [Header("Debug")]
    [SerializeField] bool _verbose = false;

    [ShowInInspector] EScenarioStep _currentStep;
    [ShowInInspector] int _currentBoothIndex = -1;
    int _currentStrengthIndex;
    int _consecutiveNegativeAnswers;
    int _validScentDataCount;

    OlfactiveCalibrationBooth _currentBooth;
    OlfactiveCalibrationBooth _approachedBooth;
    ScentDiffusionParameters _currentParameters;
    EUserResponse _wasPerceived;
    EUserResponse _wasPleasant;

    void Start()
    {
        if(!_clipsReceiverGameObject.TryGetComponent(out _clipsReceiver))
        {
            LLogger.E("ScentCalibrationScenario: ClipsReceiver does not have a valid IClipsReceiver component.");
        }

        if (_clipsReceiver == null || _player == null || _player.Head == null || _scentDiffuser == null || _olfactiveBooths.Count == 0)
        {
            LLogger.E("ScentCalibrationScenario: missing a required dependency (Argos, Player, Player.Head, ScentDiffuser or booths).");
            enabled = false;
            return;
        }

        _player.AddListener(this);

        RandomizeBoothsOrder();

        StartBoothIntroduction(0);
    }

    void OnDestroy()
    {
        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths)
        {
            if (booth.PushButton != null) booth.PushButton.RemoveListener(this);
        }

        if (_player != null) _player.RemoveListener(this);
    }

    void Update()
    {
        if (_currentStep != EScenarioStep.WaitingForApproach) return;

        OlfactiveCalibrationBooth nearbyBooth = FindNearbyBooth();
        if (nearbyBooth == _approachedBooth) return;

        _approachedBooth = nearbyBooth;
        if (nearbyBooth != null) OnBoothApproached(nearbyBooth);
    }

    // Shuffles which booth is visited in which order (the 6 strengths within a booth stay in order).
    private void RandomizeBoothsOrder()
    {
        for (int shuffleIndex = _olfactiveBooths.Count - 1; shuffleIndex > 0; shuffleIndex--)
        {
            int swapIndex = UnityEngine.Random.Range(0, shuffleIndex + 1);
            (_olfactiveBooths[shuffleIndex], _olfactiveBooths[swapIndex]) = (_olfactiveBooths[swapIndex], _olfactiveBooths[shuffleIndex]);
        }
    }

    void SetStep(EScenarioStep newStep)
    {
        if (_verbose) LLogger.LogOnScreenOnly($"ScentCalibrationScenario: {_currentStep} => {newStep}.\n" +
            $"{(_currentBooth == null ? "No booth selected" : $"({_currentBooth.name}) : {_currentBooth.ScentData.Name}")}");
        _currentStep = newStep;
    }

    // Step 1: Argos looks at the booth, it appears clearly while the others fade out, and the intro line plays.
    void StartBoothIntroduction(int boothIndex)
    {
        _currentBoothIndex = boothIndex;
        _currentBooth = _olfactiveBooths[boothIndex];
        _approachedBooth = null;

        //_argos.LookAt(_currentBooth.transform);

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths)
        {
            booth.PushButton.RemoveListener(this);
            if (booth == _currentBooth)
            {
                booth.PushButton.AddListener(this);
                booth.Appear();
            }
            else booth.Disappear();
        }

        _clipsReceiver.HandleClip("argos.calibration.ordreatelier", null);

        SetStep(EScenarioStep.WaitingForApproach);
    }

    OlfactiveCalibrationBooth FindNearbyBooth()
    {
        Vector3 headPosition = _player.Head.transform.position;

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths)
        {
            if (Vector3.Distance(headPosition, booth.transform.position) <= ApproachDistance) return booth;
        }

        return null;
    }

    // Step 2: the player approached a booth
    void OnBoothApproached(OlfactiveCalibrationBooth booth)
    {
        if (booth == _currentBooth)
        {
            _clipsReceiver.HandleClip("argos.calibration.ordrebouton");

            SetStep(EScenarioStep.WaitingForButtonPress);
        }
    }

    public void OnButtonDown()
    {
        if (_currentStep != EScenarioStep.WaitingForButtonPress) return;

        DiffuseCurrentStrength();
    }

    public void OnButtonUp()
    {
    }

    // Step 3: diffuse the current strength config for this booth's scent.
    void DiffuseCurrentStrength()
    {
        ScentData currentScentData = _currentBooth.ScentData;

        float strength = ScentStrengthsConfigs[_currentStrengthIndex];
        _currentParameters = new ScentDiffusionParameters(currentScentData.SlotIndex, strength, ScentDuration, currentScentData.DefaultVibrationFrequency);

        _wasPerceived = EUserResponse.NeutralUndecided;
        _wasPleasant = EUserResponse.NeutralUndecided;

        if (_currentBooth.DiffusionVFX != null) _currentBooth.DiffusionVFX.Play();
        _scentDiffuser.RequestDiffusion(_currentParameters);

        SetStep(EScenarioStep.Diffusing);
        StartCoroutine(WaitForPerceptionQuestionRoutine());
    }

    // Step 4: ask whether the scent was perceived, once it's had time to reach the player.
    IEnumerator WaitForPerceptionQuestionRoutine()
    {
        yield return new WaitForSeconds(PerceptionQuestionDelay);

        _clipsReceiver.HandleClip("argos.calibration.questionperception", _currentBooth.TextSpawnAnchor);
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

    // Step 5: thumb down or a flat hand means nothing more to ask, thumb up moves on to step 6.
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
                _clipsReceiver.HandleClip("argos.calibration.questionagreable", _currentBooth.TextSpawnAnchor);
                SetStep(EScenarioStep.WaitingPleasantAnswer);
                break;
        }
    }

    // Step 7: a flat hand skips straight to storing the trial, thumb up/down react and move on to the curve.
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
                _clipsReceiver.HandleClip("argos.calibration.reponseagreable", _currentBooth.TextSpawnAnchor);
                RecordResponseCurve();
                break;

            case EPlayerGesture.ThumbDown:
                _wasPleasant = EUserResponse.Negative;
                _clipsReceiver.HandleClip("argos.calibration.reponsedesagreable", _currentBooth.TextSpawnAnchor);
                RecordResponseCurve();
                break;
        }
    }

    // Steps 8-9: prompt the intensity curve, then record it (test values until the real tracing input exists).
    void RecordResponseCurve()
    {
        _currentBooth.CurveDrawingMethod.StartDraw(() => CompleteTrial());
    }

    // Step 10: store the trial on the booth's own ScentData, then either move to the next strength or the next booth.
    void CompleteTrial()
    {
        List<Vector3> curvePoints = _currentBooth.CurveDrawingMethod.GetPoints();
        ScentEvaluation evaluation = new(_currentParameters, _wasPerceived, _wasPleasant, curvePoints ?? new List<Vector3>());
        _currentBooth.ScentData.Evaluations.Add(evaluation);

        _consecutiveNegativeAnswers = _wasPleasant == EUserResponse.Negative ? _consecutiveNegativeAnswers + 1 : 0;

        _currentStrengthIndex++;

        bool allStrengthsDone = _currentStrengthIndex >= ScentStrengthsConfigs.Count;
        bool tooManyNegativeAnswers = _consecutiveNegativeAnswers >= NegativeAnswersUntilSkip;

        if (!allStrengthsDone && !tooManyNegativeAnswers)
        {
            SetStep(EScenarioStep.WaitingForButtonPress);

            _clipsReceiver.HandleClip("argos.calibration.ordrebouton", null);
            return;
        }

        MoveToNextBooth();
    }

    void MoveToNextBooth()
    {
        if (_currentBooth.ScentData.GetValidity()) _validScentDataCount++;

        _currentStrengthIndex = 0;
        _consecutiveNegativeAnswers = 0;

        int nextBoothIndex = _currentBoothIndex + 1;
        bool allBoothsVisited = nextBoothIndex >= _olfactiveBooths.Count;
        bool enoughValidScents = _validScentDataCount >= _olfactiveBooths.Count;

        if (allBoothsVisited || enoughValidScents)
        {
            EndScenario();
            return;
        }

        StartBoothIntroduction(nextBoothIndex);
    }

    void EndScenario()
    {
        SetStep(EScenarioStep.Finished);
        //_argos.StopLookAt();

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths) booth.Disappear();

        SaveResultsToJson();
    }

    // The scenario doesn't own scent data - it just collects what each booth gathered and persists it.
    void SaveResultsToJson()
    {
        ScentCalibrationResults results = new() { ScentsData = _olfactiveBooths.Select(booth => booth.ScentData).ToList() };
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
