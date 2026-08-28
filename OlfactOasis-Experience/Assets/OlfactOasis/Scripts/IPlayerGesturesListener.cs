using UnityEngine;

public interface IPlayerGesturesListener
{
    void OnGesturePerformed(EPlayerGesture gesture, ESide side, Ray direction = new());
}
