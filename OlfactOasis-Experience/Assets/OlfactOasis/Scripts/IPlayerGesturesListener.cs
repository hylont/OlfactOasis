using UnityEngine;

public interface IPlayerGesturesListener
{
    void OnGesturePerformed(EPlayerGesture gesture, ESide side = ESide.Other, Ray direction = new());
}
