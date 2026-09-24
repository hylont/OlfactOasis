using Oculus.Interaction;
using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;

// Final phase : Argos sends the participant to find the calibrated scent's product in the store ;
// grabbing it ends the experience (congratulations line, then fade to black).
public class StoreScenario : MonoBehaviour
{
    [Serializable]
    public class GrabbableScents
    {
        public EScentName ScentName;
        public Grabbable Grabbable;
    }
    [Header("Dependencies")]
    [SerializeField] Player _player;
    [SerializeField] ArgosAI _argos;
    [SerializeField] OlfyHandler _olfyHandler;
    [SerializeField] ScentCalibrationScenario _calibScenario;
    ScentDiffusionParameters _diffusionParams;
    [SerializeField] GameObject _clipsReceiverGameObject;
    IClipsReceiver _clipsReceiver;

    public List<GrabbableScents> TargetItems = new();
    GrabbableScents TargetItem = null;
    [SerializeField] ScreenFader _screenFader;

    [Header("Diffusion")]
    [SerializeField] float _minDistanceForDiffusion = 5f;
    [SerializeField] float _diffusionDelay = 20;
    float _diffusionTimer = 0;
    bool _canDiffuse = true;

    [Header("Clips")]
    [SerializeField, TextArea] string _introClip = "";
    [SerializeField, TextArea] string _nearClip = "";
    [SerializeField, TextArea] string _congratulationsClip = "";

    void Start()
    {
        if (!_clipsReceiverGameObject.TryGetComponent(out _clipsReceiver))
        {
            LLogger.E("StoreScenario: ClipsReceiver does not have a valid IClipsReceiver component.");
        }

        _clipsReceiver.HandleClip(_introClip);

        List<ScentData> optimums = _calibScenario.GetAllOptimals();

        if(optimums.Count > 0)
        {
            foreach (GrabbableScents item in TargetItems)
            {
                ScentData correspondingScent = optimums.Find(s => s.Name == item.ScentName);
                if (correspondingScent != null)
                {
                    TargetItem = item;
                    break;
                }
            }
            TargetItem.Grabbable.WhenPointerEventRaised += WhenTargetGrabbed;
            LLogger.L("Target item is " + TargetItem.ScentName);
        }
    }

    private void Update()
    {
        if(_player != null && TargetItem != null && _olfyHandler != null)
        {
            if (Vector3.Distance(_player.Head.transform.position, TargetItem.Grabbable.transform.position) < _minDistanceForDiffusion)
            {
                if (_canDiffuse)
                {
                    _diffusionTimer = 0;
                    _olfyHandler.RequestDiffusion(_diffusionParams);
                    _canDiffuse = false;
                }
                else
                {
                    _diffusionTimer += Time.deltaTime;
                    if(_diffusionTimer > _diffusionDelay)
                    {
                        _canDiffuse = true;
                    }
                }
            }
            else
            {
                _diffusionTimer = 0;
            }
        }
    }

    public void OnTeleported()
    {
        if(gameObject.activeInHierarchy && _argos != null && _player != null)
        {
            _argos.SetDestination(_player.transform.position);
        }
    }

    private void WhenTargetGrabbed(PointerEvent obj)
    {
        if(obj.Type == PointerEventType.Select)
        {
            _clipsReceiver.HandleClip(_congratulationsClip);
            _screenFader.FadeToBlack();
        }
    }

    void OnDestroy()
    {
        if (TargetItem != null) TargetItem.Grabbable.WhenPointerEventRaised -= WhenTargetGrabbed;
    }
}
