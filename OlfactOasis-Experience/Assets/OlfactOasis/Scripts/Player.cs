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


    [Header("Debug")]
    [SerializeField] bool _debug;

    List<IPlayerGesturesListener> _listeners = new();

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

    [Button("Gesture : Thumb Up")]
    void OnThumbUpPerformed()
    {
        if (_debug) LLogger.L("Thumb up recognized");

        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(EPlayerGesture.ThumbUp, ESide.Right);
        }
    }


    [Button("Gesture : Thumbs Down")]
    void OnThumbDownPerformed()
    {
        if (_debug) LLogger.L("Thumb down recognized");

        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(EPlayerGesture.ThumbDown, ESide.Right);
        }
    }
    
    [Button("Gesture : Horinzontal Hand")]
    void OnHorizontalHandPerformed()
    {
        if (_debug) LLogger.L("Horizontal hand recognized");

        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(EPlayerGesture.HorizontalHand, ESide.Right);
        }
    }

    [Button("Gesture : Arms crossed")]
    void OnArmsCrossedPerformed()
    {
        if (_debug) LLogger.L("Arms crossed recognized");

        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(EPlayerGesture.ArmsCrossed, ESide.Right);
        }
    }

    [Button("Gesture : Point finger")]
    void OnPointFingerPerformed()
    {
        if (_debug) LLogger.L("Finger pointed recognized");

        Ray direction = new Ray(RightFingerTip.transform.position, RightFingerTip.transform.forward);
        foreach (IPlayerGesturesListener listener in _listeners)
        {
            listener.OnGesturePerformed(EPlayerGesture.Pointing, ESide.Right, direction);
        }
    }
}
