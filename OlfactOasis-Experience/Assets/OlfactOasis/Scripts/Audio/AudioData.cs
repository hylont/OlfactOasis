using System;
using UnityEngine;
#if UNITY_EDITOR
#endif

[Serializable]
public class AudioData
{
    public AudioClip Clip;
    [TextArea] public string asText;
}
