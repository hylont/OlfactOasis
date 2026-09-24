using EditorAttributes;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Hands")]
    public GameObject LeftHand;
    public GameObject LeftFingerTip;
    public GameObject RightHand;
    public GameObject RightFingerTip;
    public GameObject Head;

    [Header("Cooldown")]
    public float SameGestureNotificationsCooldown = 2f;
    float _cooldownTimer = 0f;
    EPlayerGesture _cooldownedGesture = EPlayerGesture.None;

    [Header("Debug")]
    [Tooltip("While true, real hand-tracking input (HandPoseAnalyzer, grab colliders) is ignored ; interactions are only driven by the debug buttons below. Turn off once tested on-headset.")]
    public bool DebugMode = true;
    [SerializeField] bool _debug;

    public static Player Instance { get; private set; }

    List<IPlayerGesturesListener> _listeners = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            LLogger.W($"Multiple Player instances found, keeping {Instance.name}.");
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if(_cooldownedGesture != EPlayerGesture.None)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer > SameGestureNotificationsCooldown)
            {
                _cooldownedGesture = EPlayerGesture.None;
                _cooldownTimer = 0f;
            }
        }
    }

    public void AddListener(IPlayerGesturesListener listener)
    {
        if (_listeners.Contains(listener))
        {
            LLogger.W($"Observable {name} already has this listener.");
            return;
        }
        _listeners.Add(listener);
    }

    public void RemoveListener(IPlayerGesturesListener listener)
    {
        _listeners.Remove(listener);
    }

    // Shared dispatch : used by the debug buttons below AND by HandPoseAnalyzer (real hand tracking),
    // so both paths notify listeners the exact same way.
    public void NotifyGesture(EPlayerGesture gesture, ESide side = ESide.Other, Ray direction = default)
    {
        if(_cooldownedGesture == gesture)
        {
            if (_debug) LLogger.L($"Ignored {gesture} because it has just been made");
            return;
        }

        if (_debug) LLogger.L($"{gesture} recognized ({side})");

        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(gesture, side, direction);
        }

        _cooldownedGesture = gesture;
    }
    [Button("Gesture : Grab")]
    public void OnGrabPerformed() => NotifyGesture(EPlayerGesture.Grab);

    [Button("Gesture : Thumb Up")]
    public void OnThumbUpPerformed() => NotifyGesture(EPlayerGesture.ThumbUp);

    [Button("Gesture : Thumbs Down")]
    public void OnThumbDownPerformed() => NotifyGesture(EPlayerGesture.ThumbDown);

    [Button("Gesture : Horinzontal Hand")]
    public void OnHorizontalHandPerformed() => NotifyGesture(EPlayerGesture.HorizontalHand);
    
    
    public void OnLeftPinchPerformed() => NotifyGesture(EPlayerGesture.Pinch, ESide.Left);
    public void OnRightPinchPerformed() => NotifyGesture(EPlayerGesture.Pinch, ESide.Right);
    public void OnLeftPinchReleased() => NotifyGesture(EPlayerGesture.PinchReleased, ESide.Left);
    public void OnRightPinchReleased() => NotifyGesture(EPlayerGesture.PinchReleased, ESide.Right);

    [Button("Gesture : Arms crossed")]
    public void OnArmsCrossedPerformed() => NotifyGesture(EPlayerGesture.ArmsCrossed);


    public void OnLeftPointFingerPerformed()
    {
        Ray direction = new Ray(LeftFingerTip.transform.position, LeftFingerTip.transform.forward);
        NotifyGesture(EPlayerGesture.Pointing, ESide.Left, direction);
    }
    public void OnRightPointFingerPerformed()
    {
        Ray direction = new Ray(RightFingerTip.transform.position, RightFingerTip.transform.forward);
        NotifyGesture(EPlayerGesture.Pointing, ESide.Right, direction);
    }
}
