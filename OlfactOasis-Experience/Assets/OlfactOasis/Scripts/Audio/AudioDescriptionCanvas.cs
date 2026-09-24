using EditorAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class AudioDescriptionCanvas : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] AudioSource _audioSource;
    [SerializeField] TextMeshProUGUI _text;

    [Header("Text")]
    [SerializeField] int _wordsPerChunk = 6;

    [Header("Appear")]
    [SerializeField] float _appearDuration = 0.35f;
    [SerializeField] AnimationCurve _appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] Vector3 _shownScale = Vector3.one;

    [Header("Disappear")]
    [SerializeField] float _hideDelay = 2f;
    [SerializeField] float _disappearDuration = 0.35f;
    [SerializeField] AnimationCurve _disappearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] bool _verbose = false;
    RectTransform _rectTransform;
    CanvasGroup _canvasGroup;
    Coroutine _activeCoroutine;
    AudioData _lastAudioData;

    public bool IsShowing { get; private set; }

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        SetVisibility(0f, Vector3.zero);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    public void Show(AudioData audioData)
    {
        if (_audioSource == null)
        {
            LLogger.E("AudioDescriptionCanvas: no AudioSource assigned to observe");
            return;
        }

        if(audioData == null)
        {
            LLogger.E("AudioDescriptionCanvas: no AudioData provided to show");
            return;
        }

        if (audioData.Clip == null && _verbose)
        {
            LLogger.W("AudioDescriptionCanvas: AudioData or its Clip is missing");
        }

        _lastAudioData = audioData;

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(ShowRoutine(audioData));
    }

    public void Hide()
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        IsShowing = false;
        _activeCoroutine = StartCoroutine(AnimateVisibilityRoutine(false));
    }

    [Button("Repeat Last Audio")]
    public void RepeatLastAudio()
    {
        if (_lastAudioData == null)
        {
            LLogger.W("AudioDescriptionCanvas: no audio has been played yet");
            return;
        }

        Show(_lastAudioData);
    }

    IEnumerator ShowRoutine(AudioData audioData)
    {
        //LLogger.L($"AudioDescriptionCanvas: showing audio description for '{audioData.Clip?.name ?? "empty"}' and text : {audioData.AsText ?? "empty"}");
        IsShowing = true;

        if (audioData.Clip != null)
        {
            _audioSource.clip = audioData.Clip;
            _audioSource.Play();

            yield return AnimateVisibilityRoutine(true);
            yield return RevealChunkedText(_text, _audioSource, audioData.Clip, audioData.AsText, _wordsPerChunk);
        }
        else if (audioData.EstimatedSpeechDuration > 0f)
        {
            yield return AnimateVisibilityRoutine(true);
            yield return RevealChunkedTextTimed(_text, audioData.AsText, _wordsPerChunk, audioData.EstimatedSpeechDuration);
        }
        else
        {
            _text.text = audioData.AsText ?? string.Empty;
            _text.maxVisibleCharacters = _text.text.Length;

            yield return AnimateVisibilityRoutine(true);
        }

        yield return new WaitForSeconds(_hideDelay);

        yield return AnimateVisibilityRoutine(false);

        IsShowing = false;
        _activeCoroutine = null;
    }

    // Reveals `fullText` on `target`, wordsPerChunk words at a time, in sync with `audioSource` playing
    // `clip` (or instantly if clip is null - nothing to time the reveal against). Static and independent
    // of this canvas's own show/hide/fade behaviour, so any other Argos speech UI (e.g. WitTTSHandler's
    // answer text) can reuse the exact same word-by-word reveal instead of re-implementing it.
    public static IEnumerator RevealChunkedText(TextMeshProUGUI target, AudioSource audioSource, AudioClip clip, string fullText, int wordsPerChunk)
    {
        fullText ??= string.Empty;

        if (clip == null || audioSource == null)
        {
            target.text = fullText;
            target.maxVisibleCharacters = fullText.Length;
            yield break;
        }

        List<string> chunks = SplitIntoWordChunks(fullText, wordsPerChunk);
        target.text = chunks[0];
        target.maxVisibleCharacters = 0;

        int lastChunkIndex = -1;

        while (audioSource.isPlaying && audioSource.clip == clip)
        {
            float progress = clip.length > 0f ? Mathf.Clamp01(audioSource.time / clip.length) : 1f;
            lastChunkIndex = ShowChunkAtProgress(target, chunks, progress, lastChunkIndex);
            yield return null;
        }

        ShowChunkAtProgress(target, chunks, 1f, lastChunkIndex);
    }

    // Same reveal, but paced against elapsed real time instead of an AudioSource - for callers that
    // can't reliably obtain a playable AudioClip to sync against (e.g. a streaming TTS voice whose
    // clip stream doesn't expose a classic Unity AudioClip/AudioSource). Combine with
    // EstimateSpeechDuration to get a duration from the text itself.
    public static IEnumerator RevealChunkedTextTimed(TextMeshProUGUI target, string fullText, int wordsPerChunk, float duration)
    {
        fullText ??= string.Empty;

        if (duration <= 0f)
        {
            target.text = fullText;
            target.maxVisibleCharacters = fullText.Length;
            yield break;
        }

        List<string> chunks = SplitIntoWordChunks(fullText, wordsPerChunk);
        target.text = chunks[0];
        target.maxVisibleCharacters = 0;

        int lastChunkIndex = -1;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lastChunkIndex = ShowChunkAtProgress(target, chunks, Mathf.Clamp01(elapsed / duration), lastChunkIndex);
            yield return null;
        }

        ShowChunkAtProgress(target, chunks, 1f, lastChunkIndex);
    }

    // Rough speaking-time estimate from word count, for text with no audio to time against at all.
    public static float EstimateSpeechDuration(string text, float wordsPerSecond)
    {
        if (string.IsNullOrEmpty(text) || wordsPerSecond <= 0f) return 0f;

        int wordCount = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
        return wordCount / wordsPerSecond;
    }

    // Spreads words evenly across chunks (sizes differ by at most one word) instead of always
    // filling chunks to wordsPerChunk, so a trailing remainder doesn't leave the last chunk
    // noticeably shorter than the rest.
    static List<string> SplitIntoWordChunks(string text, int wordsPerChunk)
    {
        string[] words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        List<string> chunks = new List<string>();

        if (words.Length == 0)
        {
            chunks.Add(string.Empty);
            return chunks;
        }

        int chunkCount = Mathf.Max(1, Mathf.CeilToInt((float)words.Length / wordsPerChunk));
        int baseSize = words.Length / chunkCount;
        int remainder = words.Length % chunkCount;

        int index = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            int size = baseSize + (i < remainder ? 1 : 0);
            chunks.Add(string.Join(" ", words, index, size));
            index += size;
        }

        return chunks;
    }

    static int ShowChunkAtProgress(TextMeshProUGUI target, List<string> chunks, float progress, int lastChunkIndex)
    {
        float chunkPosition = progress * chunks.Count;
        int chunkIndex = Mathf.Clamp(Mathf.FloorToInt(chunkPosition), 0, chunks.Count - 1);

        if (chunkIndex != lastChunkIndex) target.text = chunks[chunkIndex];

        float chunkProgress = Mathf.Clamp01(chunkPosition - chunkIndex);
        target.maxVisibleCharacters = Mathf.FloorToInt(chunkProgress * chunks[chunkIndex].Length);

        return chunkIndex;
    }

    IEnumerator AnimateVisibilityRoutine(bool show)
    {
        float startAlpha = _canvasGroup.alpha;
        Vector3 startScale = _rectTransform.localScale;
        float targetAlpha = show ? 1f : 0f;
        Vector3 targetScale = show ? _shownScale : Vector3.zero;
        float duration = show ? _appearDuration : _disappearDuration;
        AnimationCurve curve = show ? _appearCurve : _disappearCurve;

        _canvasGroup.blocksRaycasts = show;
        _canvasGroup.interactable = show;

        if (duration <= 0f)
        {
            SetVisibility(targetAlpha, targetScale);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetVisibility(Mathf.LerpUnclamped(startAlpha, targetAlpha, t), Vector3.LerpUnclamped(startScale, targetScale, t));
            yield return null;
        }

        SetVisibility(targetAlpha, targetScale);
    }

    void SetVisibility(float alpha, Vector3 scale)
    {
        _canvasGroup.alpha = alpha;
        _rectTransform.localScale = scale;
    }
}
