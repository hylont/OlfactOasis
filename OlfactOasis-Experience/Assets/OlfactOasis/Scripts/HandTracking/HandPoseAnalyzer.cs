using System.Collections.Generic;
using UnityEngine;

// Classifies this hand's OVRSkeleton bone pose every frame into one of the gestures Player/
// IPlayerGesturesListener already knows about, and reports it through Player.NotifyGesture -
// the same entry point the debug buttons use. One instance per hand (assign _side accordingly).
//
// Detection is curl-based : for each finger, the angle between consecutive bone segments (proximal
// -> intermediate -> distal -> tip) is ~0 when straight and grows as the finger closes. A pose is
// only reported once it has been held steady for _poseHoldDuration, to avoid firing mid-transition
// between two gestures.
//
// Assumes the classic Hand_* skeleton (OVRSkeleton.SkeletonType.HandLeft/HandRight), not the newer
// XRHand_* bone set used by Unity's XR Hands package integration.
public class HandPoseAnalyzer : MonoBehaviour
{
    enum EPose
    {
        None,
        ThumbUp,
        ThumbDown,
        Horizontal,
        Pointing
    }

    [Header("Dependencies")]
    [SerializeField] OVRHand _hand;
    [SerializeField] OVRSkeleton _skeleton;
    [SerializeField] ESide _side;

    [Header("Finger curl (degrees)")]
    [Tooltip("Below this curl angle, a finger is considered extended.")]
    [SerializeField] float _extendedThreshold = 25f;
    [Tooltip("Above this curl angle, a finger is considered curled. Between the two thresholds, the finger keeps its previous state (hysteresis, avoids flicker).")]
    [SerializeField] float _curledThreshold = 50f;

    [Header("Thumb up/down")]
    [Tooltip("Minimum |dot(thumbDirection, world up)| to call it Up or Down rather than ambiguous.")]
    [SerializeField] float _thumbVerticalityThreshold = 0.6f;

    [Header("Stability")]
    [SerializeField] float _poseHoldDuration = 0.35f;

    Dictionary<OVRSkeleton.BoneId, Transform> _bones;

    bool _thumbExtended, _indexExtended, _middleExtended, _ringExtended, _pinkyExtended;

    EPose _stablePose = EPose.None;
    float _stableSince;
    bool _firedForCurrentPose;

    void Update()
    {
        if (Player.Instance == null || Player.Instance.DebugMode) return;

        if (_hand == null || _skeleton == null || !_hand.IsTracked || !_skeleton.IsInitialized)
        {
            ResetStability();
            return;
        }

        EnsureBoneMap();
        if (_bones == null) return;

        UpdateFingerStates();

        EPose candidate = ClassifyPose();

        if (candidate != _stablePose)
        {
            _stablePose = candidate;
            _stableSince = Time.time;
            _firedForCurrentPose = false;
            return;
        }

        if (_firedForCurrentPose || candidate == EPose.None) return;
        if (Time.time - _stableSince < _poseHoldDuration) return;

        FirePose(candidate);
        _firedForCurrentPose = true;
    }

    void ResetStability()
    {
        _stablePose = EPose.None;
        _firedForCurrentPose = false;
    }

    void EnsureBoneMap()
    {
        if (_bones != null) return;

        _bones = new Dictionary<OVRSkeleton.BoneId, Transform>();
        foreach (OVRBone bone in _skeleton.Bones) _bones[bone.Id] = bone.Transform;
    }

    Transform Bone(OVRSkeleton.BoneId id) => _bones.TryGetValue(id, out Transform t) ? t : null;

    void UpdateFingerStates()
    {
        _thumbExtended = UpdateExtended(_thumbExtended, ComputeCurl(OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.Hand_Thumb2, OVRSkeleton.BoneId.Hand_Thumb3, OVRSkeleton.BoneId.Hand_ThumbTip));
        _indexExtended = UpdateExtended(_indexExtended, ComputeCurl(OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3, OVRSkeleton.BoneId.Hand_IndexTip));
        _middleExtended = UpdateExtended(_middleExtended, ComputeCurl(OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.Hand_Middle3, OVRSkeleton.BoneId.Hand_MiddleTip));
        _ringExtended = UpdateExtended(_ringExtended, ComputeCurl(OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3, OVRSkeleton.BoneId.Hand_RingTip));
        _pinkyExtended = UpdateExtended(_pinkyExtended, ComputeCurl(OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3, OVRSkeleton.BoneId.Hand_PinkyTip));
    }

    // Angle between consecutive bone segments ; 0 = straight, larger = curled. Missing bones default to "straight" (0).
    float ComputeCurl(OVRSkeleton.BoneId proximalId, OVRSkeleton.BoneId intermediateId, OVRSkeleton.BoneId distalId, OVRSkeleton.BoneId tipId)
    {
        Transform proximal = Bone(proximalId);
        Transform intermediate = Bone(intermediateId);
        Transform distal = Bone(distalId);
        Transform tip = Bone(tipId);

        if (proximal == null || intermediate == null || distal == null) return 0f;

        Vector3 v1 = intermediate.position - proximal.position;
        Vector3 v2 = distal.position - intermediate.position;
        float angle = Vector3.Angle(v1, v2);

        if (tip != null)
        {
            Vector3 v3 = tip.position - distal.position;
            angle = (angle + Vector3.Angle(v2, v3)) * 0.5f;
        }

        return angle;
    }

    bool UpdateExtended(bool wasExtended, float curl)
    {
        if (wasExtended && curl > _curledThreshold) return false;
        if (!wasExtended && curl < _extendedThreshold) return true;
        return wasExtended;
    }

    EPose ClassifyPose()
    {
        bool fistExceptThumb = !_indexExtended && !_middleExtended && !_ringExtended && !_pinkyExtended;

        if (fistExceptThumb && _thumbExtended)
        {
            Transform thumbBase = Bone(OVRSkeleton.BoneId.Hand_Thumb1);
            Transform thumbTip = Bone(OVRSkeleton.BoneId.Hand_ThumbTip);

            if (thumbBase != null && thumbTip != null)
            {
                float verticality = Vector3.Dot((thumbTip.position - thumbBase.position).normalized, Vector3.up);

                if (verticality > _thumbVerticalityThreshold) return EPose.ThumbUp;
                if (verticality < -_thumbVerticalityThreshold) return EPose.ThumbDown;
            }
        }

        if (_thumbExtended && _indexExtended && _middleExtended && _ringExtended && _pinkyExtended) return EPose.Horizontal;

        if (_indexExtended && !_middleExtended && !_ringExtended && !_pinkyExtended) return EPose.Pointing;

        return EPose.None;
    }

    void FirePose(EPose pose)
    {
        switch (pose)
        {
            case EPose.ThumbUp:
                Player.Instance.NotifyGesture(EPlayerGesture.ThumbUp, _side);
                break;

            case EPose.ThumbDown:
                Player.Instance.NotifyGesture(EPlayerGesture.ThumbDown, _side);
                break;

            case EPose.Horizontal:
                Player.Instance.NotifyGesture(EPlayerGesture.HorizontalHand, _side);
                break;

            case EPose.Pointing:
                if (!_hand.IsPointerPoseValid) break;
                Ray ray = new Ray(_hand.PointerPose.position, _hand.PointerPose.forward);
                Player.Instance.NotifyGesture(EPlayerGesture.Pointing, _side, ray);
                break;
        }
    }
}
