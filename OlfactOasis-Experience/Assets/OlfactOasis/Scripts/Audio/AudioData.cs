using System;
using UnityEngine;
#if UNITY_EDITOR
#endif

[Serializable]
public class AudioData
{
    public AudioClip Clip;
    [TextArea] public string AsText;

    // Used only when Clip is null : how long (seconds) to spread the chunked text reveal over, since
    // there's no audio to time it against (e.g. a streaming TTS clip with no usable Unity AudioClip).
    // Leave at 0 to just show the full text immediately instead.
    public float EstimatedSpeechDuration;

    static readonly string DEFAULT_TEXT = "...";

    public AudioData(AudioClip clip, string asText)
    {
        Clip = clip;
        AsText = asText;
    }

    public AudioData(AudioClip clip)
    {
        Clip = clip;
        AsText = DEFAULT_TEXT;
    }
}
