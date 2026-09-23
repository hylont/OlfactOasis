using System.Collections;
using UnityEngine;

// Attached to the store item representing the calibrated scent. While the participant stays within
// ProximityDistance, diffuses the optimal intensity found during calibration (CalibrationResultHolder)
// every DiffusionInterval seconds - immediately on entering range, then on each interval after that.
public class ScentItemProximityDiffuser : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] Player _player;
    [SerializeField] OlfyHandler _scentDiffuser;

    [Header("Config")]
    [SerializeField] float _proximityDistance = 5f;
    [SerializeField] float _diffusionInterval = 20f;

    bool _isNearby;
    Coroutine _diffusionCoroutine;

    void Start()
    {
        if (_player == null) _player = Player.Instance;
        if (_scentDiffuser == null) _scentDiffuser = OlfyHandler.Instance;

        if (_player == null || _player.Head == null || _scentDiffuser == null)
        {
            LLogger.E("ScentItemProximityDiffuser: missing a required dependency (Player, Player.Head or OlfyHandler).");
            enabled = false;
        }
    }

    void Update()
    {
        bool isNearby = Vector3.Distance(_player.Head.transform.position, transform.position) <= _proximityDistance;
        if (isNearby == _isNearby) return;

        _isNearby = isNearby;

        if (isNearby) _diffusionCoroutine = StartCoroutine(DiffusionRoutine());
        else if (_diffusionCoroutine != null) StopCoroutine(_diffusionCoroutine);
    }

    IEnumerator DiffusionRoutine()
    {
        while (true)
        {
            if (CalibrationResultHolder.OptimalParameters != null)
            {
                _scentDiffuser.RequestDiffusion(CalibrationResultHolder.OptimalParameters);
            }
            else
            {
                LLogger.W("ScentItemProximityDiffuser: no optimal intensity from calibration to diffuse.");
            }

            yield return new WaitForSeconds(_diffusionInterval);
        }
    }
}
