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
    [SerializeField] string _walkableTag = "WALKABLE";
    [SerializeField] float _maxDistance = 50f;

    public UnityEvent OnTeleported;

    public bool IsTeleporting = false;

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
            _pointingRayLine.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (_player != null) _player.RemoveListener(this);
    }

    public void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = default)
    {
        if (gesture != EPlayerGesture.Pointing || IsTeleporting) return;

        if (!Physics.Raycast(direction, out RaycastHit hit, _maxDistance))
        {
            //LLogger.W("PointTeleportController: pointing ray hit nothing.");
            return;
        }

        if (!hit.collider.CompareTag(_walkableTag))
        {
            //LLogger.W($"PointTeleportController: pointing ray hit '{hit.collider.name}', which isn't tagged '{_walkableTag}'.");
            return;
        }

        if(_pointingRayLine != null)
        {
            _pointingRayLine.enabled = true;
            _pointingRayLine.SetPosition(0, direction.origin);
            _pointingRayLine.SetPosition(1, hit.point);
        }

        StartCoroutine(TeleportAfterDelay(hit.collider.gameObject.transform.position, _screenFader.FadeDuration));

        OnTeleported?.Invoke();
    }

    private IEnumerator TeleportAfterDelay(Vector3 point, float fadeDuration)
    {
        IsTeleporting = true;

        _screenFader.FadeToBlack();
        yield return new WaitForSeconds(fadeDuration);

        if (_pointingRayLine != null)
        {
            _pointingRayLine.enabled = false;
            _pointingRayLine.SetPositions(new Vector3[] { });
        }

        _screenFader.FadeToHidden();
        
        _playerRig.transform.position = point;
        IsTeleporting = false;
    }
}
