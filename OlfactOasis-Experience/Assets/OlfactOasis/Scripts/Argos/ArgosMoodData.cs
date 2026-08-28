using System;
using UnityEngine;

[Serializable]
public class ArgosMoodData
{
    [Range(0, 1f)] public float LEyeHappy = 0f;
    [Range(0, 1f)] public float LEyeBlink = 0f;
    public Vector2 LPupilPositionOffset = Vector2.zero;
    [Range(0, 1f)] public float REyeHappy = 0f;
    [Range(0, 1f)] public float REyeBlink = 0f;
    public Vector2 RPupilPositionOffset = Vector2.zero;
    public bool AllowBlink = true;

    public static ArgosMoodData Lerp(ArgosMoodData from, ArgosMoodData to, float t)
    {
        return new ArgosMoodData
        {
            LEyeHappy = Mathf.Lerp(from.LEyeHappy, to.LEyeHappy, t),
            LEyeBlink = Mathf.Lerp(from.LEyeBlink, to.LEyeBlink, t),
            LPupilPositionOffset = Vector2.Lerp(from.LPupilPositionOffset, to.LPupilPositionOffset, t),
            REyeHappy = Mathf.Lerp(from.REyeHappy, to.REyeHappy, t),
            REyeBlink = Mathf.Lerp(from.REyeBlink, to.REyeBlink, t),
            RPupilPositionOffset = Vector2.Lerp(from.RPupilPositionOffset, to.RPupilPositionOffset, t),
        };
    }
}
