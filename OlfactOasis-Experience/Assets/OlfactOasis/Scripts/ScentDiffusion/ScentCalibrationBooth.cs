using EditorAttributes;
using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine;

public class ScentCalibrationBooth : MonoBehaviour, IPlayerGesturesListener
{
    [ShowInInspector] public EScentCalibrationStep CurrentStep = EScentCalibrationStep.NONE;

    [Header("Scent data & parameters")]
    public ScentData ScentData;
    [SerializeField] ScentCalibrationScenario _masterScenario;

    [Header("Drawing method")]
    public AbstractCurveDrawingMethod CurveDrawingMethod;
    [SerializeField] List<GameObject> _curveDrawingObjects = new();

    [Header("Printer")]
    public Animator PrinterAnimator;
    [SerializeField] string _printAnimationName = "IS_PRINTING";
    public float PrintDuration = 15f;
    
    [Header("Diffusing object")]
    public Grabbable Diffuser;
    Vector3 _baseDiffuserPosition;
    Quaternion _baseDiffuserRotation;
    ScentDiffusionParameters _currentParameters;
    [ShowInInspector] bool _isPrinting = false;
    [ShowInInspector] float _printdurationTimer;
    [ShowInInspector] bool _scentDiffused = false;

    [Header("Instructions")]
    [SerializeField] GameObject IClipsReceiverGO; 
    IClipsReceiver _clipsReceiver;
    [TextArea, SerializeField] string _printingInstruction;
    [TextArea, SerializeField] string _pickupDiffuserInstruction;
    [TextArea, SerializeField] string _detectionQuestionInstruction;
    [TextArea, SerializeField] string _valenceQuestionInstruction;
    [TextArea, SerializeField] string _positiveValenceTestInstruction;
    [TextArea, SerializeField] string _negativeValenceTestInstruction;
    [TextArea, SerializeField] string _strengthSwitchInstruction;
    [TextArea, SerializeField] string _fullScentCalibrationInstruction;
    [TextArea, SerializeField] string _skipScentInstruction;

    [Header("Debug")]
    [SerializeField] bool _verbose = false;
    public List<ScentEvaluation> Evaluations;
    [ShowInInspector] int _currentStrengthIndex = 0;

    private void Start()
    {
        if(IClipsReceiverGO) _clipsReceiver = IClipsReceiverGO.GetComponent<IClipsReceiver>();

        if(CurveDrawingMethod == null || PrinterAnimator == null || Diffuser == null || _clipsReceiver == null || _masterScenario == null)
        {
            LLogger.E("Missing dependencies, aborting.");
            enabled = false;
            return;
        }

        _baseDiffuserPosition = Diffuser.transform.position;
        _baseDiffuserRotation = Diffuser.transform.rotation;

        _masterScenario.Player.AddListener(this);
    }

    private void Update()
    {
        PrinterAnimator.SetBool(_printAnimationName, _isPrinting);

        if (_isPrinting)
        {
            _printdurationTimer += Time.deltaTime;
            if(_printdurationTimer > PrintDuration)
            {
                if(CurrentStep == EScentCalibrationStep.WAIT_FOR_PRINTING)
                {
                    SetState(EScentCalibrationStep.READY);
                }
            }
        }

        if(CurrentStep == EScentCalibrationStep.READY && !_scentDiffused)
        {
            float dist = Vector3.Distance(Diffuser.transform.position, _masterScenario.Player.Head.transform.position);
            if(dist <= _masterScenario.MinDistanceFromHeadToDiffuse)
            {
                OnVialNear();
            }
        }
    }

    public void OnVialNear()
    {
        if(_currentParameters == null)
        {
            LLogger.E("Parameters of diffusion are null. Check calibration");
            return;
        }

        _masterScenario.ScentDiffuser.RequestDiffusion(_currentParameters);

        _isPrinting = true;
        _scentDiffused = true;
        SetState(EScentCalibrationStep.DETECTION_QUESTION);
    }

    public void SetState(EScentCalibrationStep newStep)
    {
        if (newStep == CurrentStep) return;

        if (_verbose) LLogger.L($"Progressed from state {CurrentStep} to {newStep}");
        CurrentStep = newStep;

        foreach (GameObject CDO in _curveDrawingObjects) CDO.SetActive(false);

        switch (CurrentStep)
        {
            case EScentCalibrationStep.WAITING:
                Diffuser.gameObject.SetActive(false);
                break;
            case EScentCalibrationStep.WAIT_FOR_PRINTING:
                _clipsReceiver.HandleClip(_printingInstruction);
                Diffuser.gameObject.SetActive(false);
                _isPrinting = true;
                break;
            case EScentCalibrationStep.READY:
                _clipsReceiver.HandleClip(_pickupDiffuserInstruction);
                Diffuser.gameObject.SetActive(true);
                Diffuser.transform.SetPositionAndRotation(_baseDiffuserPosition, _baseDiffuserRotation);
                Evaluations.Add(new ScentEvaluation());
                _isPrinting = false;
                _scentDiffused = false;
                _currentParameters = new ScentDiffusionParameters(ScentData.SlotIndex,
                    _masterScenario.ScentStrengthsConfigs[_currentStrengthIndex],
                    _masterScenario.ScentDuration,
                    ScentData.DefaultVibrationFrequency);
                break;
            case EScentCalibrationStep.DETECTION_QUESTION:
                _clipsReceiver.HandleClip(_detectionQuestionInstruction);
                Diffuser.gameObject.SetActive(false);
                break;
            case EScentCalibrationStep.VALENCE_QUESTION:
                _clipsReceiver.HandleClip(_valenceQuestionInstruction);
                break;
            case EScentCalibrationStep.VALENCE_TEST:
                foreach (GameObject CDO in _curveDrawingObjects) CDO.SetActive(true);
                if (Evaluations[_currentStrengthIndex].WasPleasant == EUserResponse.Positive)
                {
                    _clipsReceiver.HandleClip(_positiveValenceTestInstruction);
                } else if (Evaluations[_currentStrengthIndex].WasPleasant == EUserResponse.Negative)
                {
                    _clipsReceiver.HandleClip(_negativeValenceTestInstruction);
                }
                CurveDrawingMethod.StartDraw(() => OnEvaluationEnd());
                break;
        }
    }

    void OnEvaluationEnd()
    {
        Evaluations[_currentStrengthIndex].Parameters = _currentParameters;
        Evaluations[_currentStrengthIndex].ResponseCurvePoints = CurveDrawingMethod.GetPoints();
        Evaluations[_currentStrengthIndex].ResponseMagnitude = ScentEvaluation.GetResponseMagnitude(CurveDrawingMethod.GetPoints());

        //At least 2 evals, and 2 evals were unpleasant
        if (Evaluations.Count >= _masterScenario.NegativeAnswersUntilSkip
            && Evaluations.FindAll(e => e.WasPleasant == EUserResponse.Negative).Count >= _masterScenario.NegativeAnswersUntilSkip)
        {
            LLogger.W($"Calibration of {ScentData.Name} aborted");
            _clipsReceiver.HandleClip(_skipScentInstruction);
            _masterScenario.OnBoothEnded();
        }
        else
        {
            _currentStrengthIndex++;

            if (_currentStrengthIndex == _masterScenario.ScentStrengthsConfigs.Count)
            {
                LLogger.L($"Calibration for {ScentData.Name} was completed");
                //It's the master scenario who decides if the calibration continues or not, so nextbooth
                _masterScenario.OnBoothEnded();
            }
            else
            {
                _clipsReceiver.HandleClip(_strengthSwitchInstruction);
                SetState(EScentCalibrationStep.WAIT_FOR_PRINTING);
            }
        }        
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side = ESide.Other, Ray direction = default)
    {
        if(CurrentStep == EScentCalibrationStep.DETECTION_QUESTION)
        {
            if (gesture == EPlayerGesture.ThumbUp)
            {
                Evaluations[_currentStrengthIndex].WasPerceived = EUserResponse.Positive;

                SetState(EScentCalibrationStep.VALENCE_QUESTION);
            }
            else
            {
                if (gesture == EPlayerGesture.ThumbDown)
                {
                    Evaluations[_currentStrengthIndex].WasPerceived = EUserResponse.Negative;
                    OnEvaluationEnd();
                
                }else if (gesture == EPlayerGesture.HorizontalHand)
                {
                    Evaluations[_currentStrengthIndex].WasPerceived = EUserResponse.NeutralUndecided;
                    OnEvaluationEnd();
                }
            }
        }

        if (CurrentStep == EScentCalibrationStep.VALENCE_QUESTION)
        {
            if (gesture == EPlayerGesture.HorizontalHand)
            {
                Evaluations[_currentStrengthIndex].WasPleasant = EUserResponse.NeutralUndecided;
                OnEvaluationEnd();
            }
            else
            {
                if (gesture == EPlayerGesture.ThumbUp)
                {
                    Evaluations[_currentStrengthIndex].WasPleasant = EUserResponse.Positive;
                    SetState(EScentCalibrationStep.VALENCE_TEST);
                }else if (gesture == EPlayerGesture.ThumbDown)
                {
                    Evaluations[_currentStrengthIndex].WasPleasant = EUserResponse.Negative;
                    SetState(EScentCalibrationStep.VALENCE_TEST);
                }
            }
        }
    }
}