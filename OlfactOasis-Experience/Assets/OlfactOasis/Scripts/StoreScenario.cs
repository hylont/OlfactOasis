using Oculus.Interaction;
using UnityEngine;

// Final phase : Argos sends the participant to find the calibrated scent's product in the store ;
// grabbing it ends the experience (congratulations line, then fade to black).
public class StoreScenario : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;
    [SerializeField] Oculus.Interaction.Grabbable _targetItem;
    [SerializeField] ScreenFader _screenFader;

    [Header("Clips")]
    [SerializeField] string _introClipID = "argos.magasin.consigne";
    [SerializeField] string _congratulationsClipID = "argos.magasin.felicitations";

    void Start()
    {
        if (!_clipsReceiverGameObject.TryGetComponent(out _clipsReceiver))
        {
            LLogger.E("StoreScenario: ClipsReceiver does not have a valid IClipsReceiver component.");
        }

        if (_clipsReceiver == null || _targetItem == null || _screenFader == null)
        {
            LLogger.E("StoreScenario: missing a required dependency (Argos, target item or ScreenFader).");
            return;
        }

        _targetItem.WhenPointerEventRaised += OnGrabbed;

        _clipsReceiver.HandleClip(_introClipID);
    }

    void OnDestroy()
    {
        if (_targetItem != null) _targetItem.WhenPointerEventRaised -= OnGrabbed;
    }

    public void OnGrabbed(PointerEvent @event)
    {
        if(@event.Type != PointerEventType.Select) return;

        _clipsReceiver.HandleClip(_congratulationsClipID);
        _screenFader.FadeToBlack();
    }
}
