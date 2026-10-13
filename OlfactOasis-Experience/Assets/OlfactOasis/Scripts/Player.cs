using EditorAttributes;
using Oculus.Interaction;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    // Property names of the Oculus hand shader
    const string TopColorProperty = "_ColorTop";
    const string BottomColorProperty = "_ColorBottom";
    const string OpacityProperty = "_Opacity";

    [Header("Left hand")]
    public GameObject LeftHand;
    public GameObject LeftFingerTip;
    [SerializeField] MaterialPropertyBlockEditor _leftHandMaterial;

    [Header("Right hand")]
    public GameObject RightHand;
    public GameObject RightFingerTip;
    [SerializeField] MaterialPropertyBlockEditor _rightHandMaterial;

    [Header("Head")]
    public GameObject Head;

    [Header("Cooldown")]
    public float GestureNotificationsCooldown = 2f;
    float _cooldownTimer = 0f;
    EPlayerGesture _cooldownedGesture = EPlayerGesture.None;

    [Serializable]
    public class HandVisualsParams
    {
        [Header("Hand")]
        public Color TopColor = Color.white;
        public Color BottomColor = Color.gray;
        public float Opacity = .9f;
    }
    [Serializable]
    public class GestureConfig
    {
        public EPlayerGesture Gesture;
        public HandVisualsParams Visuals;
        public ActiveStateUnityEventWrapper LeftEventWrapper;
        public ActiveStateUnityEventWrapper RightEventWrapper;
        [Tooltip("Gesture notified when a validated pose ends (e.g. PinchReleased for Pinch). None to notify nothing.")]
        public EPlayerGesture ReleaseGesture = EPlayerGesture.None;
        [Tooltip("The pose and its release are neither blocked by the cooldowns nor start them (continuous interactions such as pinch drawing).")]
        public bool IgnoreCooldown = false;
    }

    // Pose currently held by a hand
    class HeldPose
    {
        public GestureConfig Config;
        public HandVisualsParams StartVisuals;
        public float Timer;
        public bool Validated;
    }

    [Header("Gestures")]
    [Tooltip("Time a pose must be held before being validated. Meanwhile, the hand visuals blend towards the gesture ones.")]
    [SerializeField] float _poseHoldDuration = 2f;
    [Tooltip("Visuals of a hand attempting a pose while the cooldown of the last validated pose is running.")]
    [SerializeField] HandVisualsParams _cooldownVisuals;
    [SerializeField] List<GestureConfig> _gestures;

    [Header("Debug")]
    [Tooltip("While true, real hand-tracking input (HandPoseAnalyzer, grab colliders) is ignored ; interactions are only driven by the debug buttons below. Turn off once tested on-headset.")]
    [SerializeField] bool _debug;

    public static Player Instance { get; private set; }

    List<IPlayerGesturesListener> _listeners = new();
    Dictionary<ESide, HeldPose> _heldPoses = new();
    HandVisualsParams _defaultVisuals;
    float _poseCooldownTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            LLogger.W($"Multiple Player instances found, keeping {Instance.name}.");
            return;
        }

        Instance = this;

        _defaultVisuals = _gestures.Find(g => g.Gesture == EPlayerGesture.None)?.Visuals;
        if(_defaultVisuals == null)
        {
            LLogger.W("There is no gesture config for EPlayerGesture.None, default hand visuals will be used.");
            _defaultVisuals = new();
        }

        foreach (ESide side in new[] { ESide.Left, ESide.Right })
        {
            // The MaterialPropertyBlockEditor rewrites its properties into the block every frame :
            // they are removed so the hand visuals are only driven from here.
            GetHandMaterial(side).ColorProperties.Clear();
            GetHandMaterial(side).FloatProperties.Clear();
            SetVisuals(side, _defaultVisuals);
        }

        foreach (GestureConfig config in _gestures)
        {
            ListenToPose(config, config.LeftEventWrapper, ESide.Left);
            ListenToPose(config, config.RightEventWrapper, ESide.Right);
        }
    }

    private void Update()
    {
        if(_cooldownedGesture != EPlayerGesture.None)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer > GestureNotificationsCooldown)
            {
                _cooldownedGesture = EPlayerGesture.None;
                _cooldownTimer = 0f;
            }
        }

        UpdateHeldPoses();
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
    public void NotifyGesture(EPlayerGesture gesture, ESide side = ESide.Other, Ray direction = default, bool ignoreCooldown = false)
    {
        if(!ignoreCooldown && _cooldownedGesture == gesture)
        {
            if (_debug) LLogger.L($"Ignored {gesture} because it has just been made");
            return;
        }

        if (_debug) LLogger.L($"{gesture} recognized ({side})");

        // Iterates over a copy : a listener may remove itself (e.g. a booth ending on a thumb down)
        foreach (IPlayerGesturesListener listener in _listeners.ToArray())
        {
            listener.OnGesturePerformed(gesture, side, direction);
        }

        if (ignoreCooldown) return;
        _cooldownedGesture = gesture;
        _cooldownTimer = 0f;
    }

    void ListenToPose(GestureConfig config, ActiveStateUnityEventWrapper wrapper, ESide side)
    {
        if (wrapper == null) return;

        wrapper.WhenActivated.AddListener(() => OnPoseStarted(config, side));
        wrapper.WhenDeactivated.AddListener(() => OnPoseEnded(config, side));
    }

    void OnPoseStarted(GestureConfig config, ESide side)
    {
        if (_debug) return;

        // The new pose may activate before the previous one deactivates : its release must not be lost
        if (_heldPoses.TryGetValue(side, out HeldPose previousPose)) NotifyRelease(side, previousPose);

        _heldPoses[side] = new HeldPose { Config = config };
        if (_poseCooldownTimer > 0f && !config.IgnoreCooldown) SetVisuals(side, _cooldownVisuals);
    }

    void OnPoseEnded(GestureConfig config, ESide side)
    {
        // Ignored when the hand has already switched to another pose
        if (!_heldPoses.TryGetValue(side, out HeldPose pose) || pose.Config != config) return;

        _heldPoses.Remove(side);
        SetVisuals(side, _defaultVisuals);
        NotifyRelease(side, pose);
    }

    void NotifyRelease(ESide side, HeldPose pose)
    {
        // Only a validated pose has been notified, so only it has something to release
        if (!pose.Validated || pose.Config.ReleaseGesture == EPlayerGesture.None) return;

        NotifyGesture(pose.Config.ReleaseGesture, side, ignoreCooldown: pose.Config.IgnoreCooldown);
    }

    void UpdateHeldPoses()
    {
        bool cooldownRunning = _poseCooldownTimer > 0f;
        if (cooldownRunning) _poseCooldownTimer -= Time.deltaTime;

        foreach ((ESide side, HeldPose pose) in _heldPoses)
        {
            if (pose.Validated) continue;
            if (cooldownRunning && !pose.Config.IgnoreCooldown) continue;

            // Blends from the visuals the hand has when the hold starts (default, previous pose or cooldown ones)
            pose.StartVisuals ??= ReadVisuals(side);
            pose.Timer += Time.deltaTime;
            float progress = Mathf.Clamp01(pose.Timer / _poseHoldDuration);
            LerpVisuals(side, pose.StartVisuals, pose.Config.Visuals, progress);

            if (progress >= 1f)
            {
                ValidatePose(side, pose);
                return;
            }
        }
    }

    void ValidatePose(ESide side, HeldPose pose)
    {
        pose.Validated = true;

        if (!pose.Config.IgnoreCooldown)
        {
            _poseCooldownTimer = GestureNotificationsCooldown;

            // A pose being held by the other hand has to be held again from the start once the cooldown is over
            ESide otherSide = side == ESide.Left ? ESide.Right : ESide.Left;
            if (_heldPoses.TryGetValue(otherSide, out HeldPose otherPose) && !otherPose.Validated && !otherPose.Config.IgnoreCooldown)
            {
                otherPose.Timer = 0f;
                otherPose.StartVisuals = null;
                SetVisuals(otherSide, _cooldownVisuals);
            }
        }

        EPlayerGesture gesture = pose.Config.Gesture;
        NotifyGesture(gesture, side, gesture == EPlayerGesture.Pointing ? GetPointingRay(side) : default, pose.Config.IgnoreCooldown);
    }

    MaterialPropertyBlockEditor GetHandMaterial(ESide side) => side == ESide.Left ? _leftHandMaterial : _rightHandMaterial;

    void SetVisuals(ESide side, HandVisualsParams visuals) => LerpVisuals(side, visuals, visuals, 1f);

    void LerpVisuals(ESide side, HandVisualsParams from, HandVisualsParams to, float t)
    {
        MaterialPropertyBlockEditor material = GetHandMaterial(side);
        material.MaterialPropertyBlock.SetColor(TopColorProperty, Color.Lerp(from.TopColor, to.TopColor, t));
        material.MaterialPropertyBlock.SetColor(BottomColorProperty, Color.Lerp(from.BottomColor, to.BottomColor, t));
        material.MaterialPropertyBlock.SetFloat(OpacityProperty, Mathf.Lerp(from.Opacity, to.Opacity, t));
        material.UpdateMaterialPropertyBlock();
    }

    HandVisualsParams ReadVisuals(ESide side)
    {
        MaterialPropertyBlock block = GetHandMaterial(side).MaterialPropertyBlock;
        return new()
        {
            TopColor = block.GetColor(TopColorProperty),
            BottomColor = block.GetColor(BottomColorProperty),
            Opacity = block.GetFloat(OpacityProperty),
        };
    }

    public Ray GetPointingRay(ESide side)
    {
        Transform fingerTip = (side == ESide.Left ? LeftFingerTip : RightFingerTip).transform;
        return new Ray(fingerTip.position, fingerTip.forward);
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


    public void OnLeftPointFingerPerformed() => NotifyGesture(EPlayerGesture.Pointing, ESide.Left, GetPointingRay(ESide.Left));
    public void OnRightPointFingerPerformed() => NotifyGesture(EPlayerGesture.Pointing, ESide.Right, GetPointingRay(ESide.Right));
}
