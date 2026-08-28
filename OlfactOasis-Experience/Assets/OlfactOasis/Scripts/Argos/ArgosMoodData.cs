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

    public static ArgosMoodData Lerp(ArgosMoodData p_from, ArgosMoodData p_to, float p_t)
    {
        return new ArgosMoodData
        {
            LEyeHappy = Mathf.Lerp(p_from.LEyeHappy, p_to.LEyeHappy, p_t),
            LEyeBlink = Mathf.Lerp(p_from.LEyeBlink, p_to.LEyeBlink, p_t),
            LPupilPositionOffset = Vector2.Lerp(p_from.LPupilPositionOffset, p_to.LPupilPositionOffset, p_t),
            REyeHappy = Mathf.Lerp(p_from.REyeHappy, p_to.REyeHappy, p_t),
            REyeBlink = Mathf.Lerp(p_from.REyeBlink, p_to.REyeBlink, p_t),
            RPupilPositionOffset = Vector2.Lerp(p_from.RPupilPositionOffset, p_to.RPupilPositionOffset, p_t),
        };
    }
}
