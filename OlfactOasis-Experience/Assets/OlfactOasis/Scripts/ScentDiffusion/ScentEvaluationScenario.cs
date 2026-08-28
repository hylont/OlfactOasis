using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScentEvaluationScenario : MonoBehaviour, IButtonListener, IPlayerGesturesListener
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

    [Header("Protocol config")]
    //1x3, 3x3, 7x3, 20x3, 55x3, 100x3
    public List<float> ScentStrengthsConfigs = new() { .01f, .03f, .07f, .2f, .55f, 1f };
    public float ScentDuration = 3f;
    public float PerceptionQuestionDelay = 10f;
    public float ApproachDistance = 1.5f;

    public int NegativeAnswersUntilSkip = 3;

    public List<ScentData> ScentsData = new();


    [Header("Booths containing a push button")]
    [SerializeField] List<OlfactiveCalibrationBooth> _olfactiveBooths = new();

    [Header("Dependencies")]
    [SerializeField] ArgosAI _argos;
    [SerializeField] Player _player;
    [SerializeField] OlfyHandler _scentDiffuser;

    readonly Dictionary<ScentData, OlfactiveCalibrationBooth> _boothsByScentData = new();

    EScenarioStep _currentStep;
    int _currentScentIndex = -1;
    int _currentStrengthIndex;
    int _consecutiveNegativeAnswers;
    int _validScentDataCount;

    ScentData _currentScentData;
    OlfactiveCalibrationBooth _currentBooth;
    OlfactiveCalibrationBooth _approachedBooth;
    ScentDiffusionParameters _currentParameters;
    EUserResponse _wasPerceived;
    EUserResponse _wasPleasant;

    void Start()
    {
        if (_argos == null || _player == null || _player.Head == null || _scentDiffuser == null || _olfactiveBooths.Count == 0)
        {
            LLogger.E("ScentEvaluationScenario: missing a required dependency (Argos, Player, Player.Head, ScentDiffuser or booths).");
            enabled = false;
            return;
        }

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths)
        {
            _boothsByScentData[booth.ScentData] = booth;
            booth.PushButton.AddListener(this);
        }

        _player.AddListener(this);

        ScentsData = _olfactiveBooths.Select(booth => booth.ScentData).ToList();
        RandomizeScentsDataOrder();

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

    private void RandomizeScentsDataOrder()
    {
        for (int shuffleIndex = ScentsData.Count - 1; shuffleIndex > 0; shuffleIndex--)
        {
            int swapIndex = Random.Range(0, shuffleIndex + 1);
            (ScentsData[shuffleIndex], ScentsData[swapIndex]) = (ScentsData[swapIndex], ScentsData[shuffleIndex]);
        }
    }

    // Step 1: Argos looks at the booth, it appears clearly while the others fade out, and the intro line plays.
    void StartBoothIntroduction(int scentIndex)
    {
        _currentScentIndex = scentIndex;
        _currentScentData = ScentsData[scentIndex];
        _currentBooth = _boothsByScentData[_currentScentData];
        _approachedBooth = null;

        _argos.LookAt(_currentBooth.transform);

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths)
        {
            if (booth == _currentBooth) booth.Appear();
            else booth.Disappear();
        }

        _argos.Talk("argos.calibration.ordreatelier");

        _currentStep = EScenarioStep.WaitingForApproach;
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

    // Step 2: the player approached a booth, tell them whether it's the right one.
    void OnBoothApproached(OlfactiveCalibrationBooth booth)
    {
        if (booth == _currentBooth)
        {
            _argos.Talk("argos.calibration.ordrebouton");
            _currentStep = EScenarioStep.WaitingForButtonPress;
        }
        else
        {
            _argos.Talk("argos.calibration.mauvaisatelier");
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

    // Step 3: diffuse the current strength config for this scent.
    void DiffuseCurrentStrength()
    {
        float strength = ScentStrengthsConfigs[_currentStrengthIndex];
        _currentParameters = new ScentDiffusionParameters(_currentScentData.SlotIndex, strength, ScentDuration, _currentScentData.DefaultVibrationFrequency);

        _wasPerceived = EUserResponse.NeutralUndecided;
        _wasPleasant = EUserResponse.NeutralUndecided;

        if (_currentBooth.DiffusionVFX != null) _currentBooth.DiffusionVFX.Play();
        _scentDiffuser.RequestDiffusion(_currentParameters);

        _currentStep = EScenarioStep.Diffusing;
        StartCoroutine(WaitForPerceptionQuestionRoutine());
    }

    // Step 4: ask whether the scent was perceived, once it's had time to reach the player.
    IEnumerator WaitForPerceptionQuestionRoutine()
    {
        yield return new WaitForSeconds(PerceptionQuestionDelay);

        _argos.Talk("argos.calibration.questionodeurpercue");
        _currentStep = EScenarioStep.WaitingPerceivedAnswer;
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
                _argos.Talk("argos.calibration.questionagreable");
                _currentStep = EScenarioStep.WaitingPleasantAnswer;
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
                _argos.Talk("argos.calibration.reponseagreable");
                RecordResponseCurve();
                break;

            case EPlayerGesture.ThumbDown:
                _wasPleasant = EUserResponse.Negative;
                _argos.Talk("argos.calibration.reponsedesagreable");
                RecordResponseCurve();
                break;
        }
    }

    // Steps 8-9: prompt the intensity curve, then record it (test values until the real tracing input exists).
    void RecordResponseCurve()
    {
        _argos.Talk("argos.calibration.boutonserreur");

        List<Vector3> testCurvePoints = new()
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0.3f, 0f),
            new Vector3(2f, 0.8f, 0f),
            new Vector3(3f, 0.4f, 0f),
            new Vector3(4f, 0.1f, 0f)
        };

        CompleteTrial(testCurvePoints);
    }

    // Step 10: store the trial, then either move to the next strength or the next booth.
    void CompleteTrial(List<Vector3> curvePoints = null)
    {
        ScentEvaluation evaluation = new(_currentParameters, _wasPerceived, _wasPleasant, curvePoints ?? new List<Vector3>());
        _currentScentData.Evaluations.Add(evaluation);

        _consecutiveNegativeAnswers = _wasPleasant == EUserResponse.Negative ? _consecutiveNegativeAnswers + 1 : 0;

        _currentStrengthIndex++;

        bool allStrengthsDone = _currentStrengthIndex >= ScentStrengthsConfigs.Count;
        bool tooManyNegativeAnswers = _consecutiveNegativeAnswers >= NegativeAnswersUntilSkip;

        if (!allStrengthsDone && !tooManyNegativeAnswers)
        {
            _currentStep = EScenarioStep.WaitingForButtonPress;
            return;
        }

        MoveToNextBooth();
    }

    void MoveToNextBooth()
    {
        if (_currentScentData.GetValidity()) _validScentDataCount++;

        _currentStrengthIndex = 0;
        _consecutiveNegativeAnswers = 0;

        int nextScentIndex = _currentScentIndex + 1;
        bool allBoothsVisited = nextScentIndex >= ScentsData.Count;
        bool enoughValidScents = _validScentDataCount >= ScentsData.Count;

        if (allBoothsVisited || enoughValidScents)
        {
            EndScenario();
            return;
        }

        StartBoothIntroduction(nextScentIndex);
    }

    void EndScenario()
    {
        _currentStep = EScenarioStep.Finished;
        _argos.StopLookAt();

        foreach (OlfactiveCalibrationBooth booth in _olfactiveBooths) booth.Disappear();

        LLogger.L($"ScentEvaluationScenario: scenario termine avec {_validScentDataCount} odeur(s) valide(s) sur {ScentsData.Count}.");
    }
}
