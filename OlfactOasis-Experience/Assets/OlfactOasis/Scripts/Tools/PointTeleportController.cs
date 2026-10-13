using EditorAttributes;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Listens for the Pointing gesture (Player.cs) and teleports the player rig to wherever the
// pointing ray hits a WALKABLE-tagged surface (same tag convention as ForceMoveAgent's click-to-move).
public class PointTeleportController : MonoBehaviour, IPlayerGesturesListener
{
    [Header("Dependencies")]
    [SerializeField] GameObject _playerRig;
    [SerializeField] Player _player;
    [SerializeField] LineRenderer _pointingRayLine;
    [SerializeField] ScreenFader _screenFader;

    [Header("Config")]
    [SerializeField] GameObject _baseTeleporter;
    [SerializeField] string _walkableTag = "WALKABLE";
    [SerializeField] float _maxDistance = 50f;

    public UnityEvent OnTeleported;

    public bool IsTeleporting = false;
    [ShowInInspector] bool _isPointing = false;
    [ShowInInspector] ESide hand = ESide.Other;

    GameObject _currentTeleporter = null;

    void Start()
    {
        if (_player == null) _player = Player.Instance;

        if (_player == null)
        {
            LLogger.E("no Player found.");
            enabled = false;
            return;
        }

        _player.AddListener(this);

        if(_playerRig == null)
        {
            LLogger.E("no Player Rig assigned.");
            enabled = false;
            return;
        }

        if (_pointingRayLine == null)
        {
            LLogger.W("no LineRenderer assigned for the pointing ray.");
        }
        else
        {
            // The ray is given in world coordinates (P_RayLine is authored in local space)
            _pointingRayLine.useWorldSpace = true;
            _pointingRayLine.positionCount = 2;
            _pointingRayLine.enabled = false;
        }

        if (_baseTeleporter != null) _currentTeleporter = _baseTeleporter;
    }

    void Update()
    {
        if(!_isPointing || hand == ESide.Other || IsTeleporting)
        {
            if (_pointingRayLine != null) _pointingRayLine.enabled = false;
            return;
        }

        Ray ray = _player.GetPointingRay(hand);

        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance))
        {

            if (_pointingRayLine != null)
            {
                ShowLineRenderer(ray, hit);
            }

            if (hit.collider.CompareTag(_walkableTag))
            {
                GameObject target = hit.collider.gameObject;
                LLogger.L($"Teleported from {(_currentTeleporter == null ? "nowhere" : _currentTeleporter.name)} to {target.name}");

                if (target == _currentTeleporter) return;
                _currentTeleporter = target;

                StartCoroutine(TeleportAfterDelay(target.transform.position, _screenFader.FadeDuration));

                OnTeleported?.Invoke();
            }
        }
    }

    private void ShowLineRenderer(Ray ray, RaycastHit hit)
    {
        _pointingRayLine.enabled = true;
        _pointingRayLine.SetPosition(0, ray.origin);
        _pointingRayLine.SetPosition(1, hit.point);
    }

    void OnDestroy()
    {
        if (_player != null) _player.RemoveListener(this);
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = default)
    {
        if (IsTeleporting) return;

        if(gesture == EPlayerGesture.Pointing)
        {
            _isPointing = true;
            hand = side;
        }
        else if(gesture == EPlayerGesture.PointingReleased && _isPointing)
        {
            _isPointing = false;
            hand = ESide.Other;

            if (_pointingRayLine != null)
            {
                _pointingRayLine.enabled = false;
                _pointingRayLine.SetPositions(null);
            }
        }
    }

    private IEnumerator TeleportAfterDelay(Vector3 point, float fadeDuration)
    {
        IsTeleporting = true;

        _screenFader.FadeToBlack();
        yield return new WaitForSeconds(fadeDuration);

        if (_pointingRayLine != null) _pointingRayLine.enabled = false;

        _screenFader.FadeToHidden();
        
        _playerRig.transform.position = point;
        IsTeleporting = false;
    }
}
