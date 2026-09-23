using EditorAttributes;
using NUnit.Framework;
using Oculus.Interaction;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using UnityEngine;
using UnityEngine.Events;

// Argos walks the participant through the 6 interactions needed for the rest of the experience,
// one at a time, each validated before moving on to the next. Same step-machine shape as
// ScentCalibrationScenario. Started explicitly via Begin() (wired from LoadingScreenController)
// rather than on Start(), since it now runs after the loading screen instead of first thing.
public partial class TutorialScenario : MonoBehaviour, IPlayerGesturesListener
{

    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;
    [SerializeField] Player _player;
    [SerializeField] AbstractCurveDrawingMethod _curveDrawingMethod;
    [SerializeField] PointTeleportController _teleportController;
    [SerializeField] Oculus.Interaction.Grabbable _grabbableFigurine;


    [Header("Helpers")]
    [TextArea, SerializeField] string _introClipID = "argos.tutoriel.intro";
    [TextArea, SerializeField] string _grabClipID = "argos.tutoriel.grab";
    [SerializeField] GameObject _grabHelper;
    [TextArea, SerializeField] string _thumbUpClipID = "argos.tutoriel.poucehaut";
    [SerializeField] GameObject _thumbUpHelper;
    [TextArea, SerializeField] string _thumbDownClipID = "argos.tutoriel.poucebas";
    [SerializeField] GameObject _thumbDownHelper;
    [TextArea, SerializeField] string _horizontalClipID = "argos.tutoriel.horizontal";
    [SerializeField] GameObject _horizontalHelper;
    [TextArea, SerializeField] string _drawClipID = "argos.tutoriel.dessin";
    [SerializeField] GameObject _drawHelper;
    [TextArea, SerializeField] string _teleportClipID = "argos.tutoriel.teleportation";
    [SerializeField] GameObject _teleportHelper;
    [TextArea, SerializeField] string _endClipID = "argos.tutoriel.fin";

    [ShowInInspector] ETutorialStep _currentStep = ETutorialStep.NotStarted;

    public UnityEvent OnTutorialComplete;

    public void Begin()
    {
        if (!_clipsReceiverGameObject.TryGetComponent(out _clipsReceiver))
        {
            LLogger.E("ClipsReceiver does not have a valid IClipsReceiver component.");
        }

        if (_clipsReceiver == null || _player == null || _grabbableFigurine == null || _curveDrawingMethod == null || _teleportController == null)
        {
            LLogger.E("missing a required dependency (Argos, Player, figurine, curve drawing method or teleport controller).");
            return;
        }

        _player.AddListener(this);
        _teleportController.OnTeleported.AddListener(OnTeleportValidated);

        _clipsReceiver.HandleClip(_introClipID);
        SetStep(ETutorialStep.Grab);
    }

    void OnDestroy()
    {
        if (_player != null) _player.RemoveListener(this);
        if (_teleportController != null) _teleportController.OnTeleported.RemoveListener(OnTeleportValidated);
        if (_grabbableFigurine != null) _grabbableFigurine.WhenPointerEventRaised -= OnGrabbed;
    }

    void SetStep(ETutorialStep newStep)
    {
        _currentStep = newStep;

        if (_grabHelper != null) _grabHelper.SetActive(false);
        if (_thumbUpHelper != null) _thumbUpHelper.SetActive(false);
        if (_thumbDownHelper != null) _thumbDownHelper.SetActive(false);
        if (_horizontalHelper != null) _horizontalHelper.SetActive(false);
        if (_drawHelper != null) _drawHelper.SetActive(false);
        if (_teleportHelper != null) _teleportHelper.SetActive(false);

        switch (newStep)
        {
            case ETutorialStep.Grab:
                _clipsReceiver.HandleClip(_grabClipID);
                if (_grabHelper != null) _grabHelper.SetActive(true);
                _grabbableFigurine.WhenPointerEventRaised += OnGrabbed;
                break;

            case ETutorialStep.ThumbUp:
                _clipsReceiver.HandleClip(_thumbUpClipID);
                if(_thumbUpHelper != null) _thumbUpHelper.SetActive(true);
                break;

            case ETutorialStep.ThumbDown:
                _clipsReceiver.HandleClip(_thumbDownClipID);
                if(_thumbDownHelper != null) _thumbDownHelper.SetActive(true);
                break;

            case ETutorialStep.Horizontal:
                _clipsReceiver.HandleClip(_horizontalClipID);
                if(_horizontalHelper != null) _horizontalHelper.SetActive(true);
                break;

            case ETutorialStep.Draw:
                _clipsReceiver.HandleClip(_drawClipID);
                if(_drawHelper != null) _drawHelper.SetActive(true);
                _curveDrawingMethod.StartDraw(OnDrawEnded);
                break;

            case ETutorialStep.Teleport:
                _clipsReceiver.HandleClip(_teleportClipID);
                if(_teleportHelper != null) _teleportHelper.SetActive(true);
                break;

            case ETutorialStep.Finished:
                _clipsReceiver.HandleClip(_endClipID);
                OnTutorialComplete?.Invoke();
                break;
        }
    }

    private void OnGrabbed(PointerEvent @event)
    {
        if(@event.Type != PointerEventType.Select) return;
        
        SetStep(ETutorialStep.ThumbUp);
    }

    public void OnGrabbed()
    {
        if (_currentStep != ETutorialStep.Grab) return;

        SetStep(ETutorialStep.ThumbUp);
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = default)
    {
        switch (_currentStep)
        {
            case ETutorialStep.ThumbUp:
                if (gesture == EPlayerGesture.ThumbUp) SetStep(ETutorialStep.ThumbDown);
                break;

            case ETutorialStep.ThumbDown:
                if (gesture == EPlayerGesture.ThumbDown) SetStep(ETutorialStep.Horizontal);
                break;

            case ETutorialStep.Horizontal:
                if (gesture == EPlayerGesture.HorizontalHand) SetStep(ETutorialStep.Draw);
                break;
        }
    }

    void OnDrawEnded()
    {
        if (_currentStep != ETutorialStep.Draw) return;

        // Too short a trace (a stray click) doesn't count - let the participant try again.
        if (_curveDrawingMethod.GetPoints().Count < 2)
        {
            _curveDrawingMethod.StartDraw(OnDrawEnded);
            return;
        }

        SetStep(ETutorialStep.Teleport);
    }

    void OnTeleportValidated()
    {
        if (_currentStep != ETutorialStep.Teleport) return;

        SetStep(ETutorialStep.Finished);
    }
}
