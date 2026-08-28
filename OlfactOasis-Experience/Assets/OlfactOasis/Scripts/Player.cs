using EditorAttributes;
using UnityEngine;

public class Player : MonoBehaviour
{

    [Header("Debug")]
    [SerializeField] bool _debug;

    [Button("Gesture : Thumb Up")]
    void OnThumbUp()
    {
        if (_debug) LLogger.L("Thumb up recognized");
    }


    [Button("Gesture : Thumbs Down")]
    void OnThumbDown()
    {
        if (_debug) LLogger.L("Thumb down recognized");
    }

    [Button("Gesture : Arms crossed")]
    void OnArmsCrossed()
    {
        if (_debug) LLogger.L("Arms crossed recognized");
    }

    [Button("Gesture : Point finger")]
    void OnPointFinger()
    {
        if (_debug) LLogger.L("Finger pointed recognized");
    }
}
