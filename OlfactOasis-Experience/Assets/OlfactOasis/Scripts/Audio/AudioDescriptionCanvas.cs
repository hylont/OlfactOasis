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
        if (audioData == null || audioData.Clip == null)
        {
            LLogger.E("AudioDescriptionCanvas: AudioData or its Clip is missing");
            return;
        }

        if (_audioSource == null)
        {
            LLogger.E("AudioDescriptionCanvas: no AudioSource assigned to observe");
            return;
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
        IsShowing = true;

        _audioSource.clip = audioData.Clip;
        _audioSource.Play();

        string description = audioData.AsText ?? string.Empty;
        List<string> chunks = SplitIntoWordChunks(description, _wordsPerChunk);
        _text.text = chunks[0];
        _text.maxVisibleCharacters = 0;

        yield return AnimateVisibilityRoutine(true);
        yield return RevealTextRoutine(audioData.Clip, chunks);

        yield return new WaitForSeconds(_hideDelay);

        yield return AnimateVisibilityRoutine(false);

        IsShowing = false;
        _activeCoroutine = null;
    }

    // Spreads words evenly across chunks (sizes differ by at most one word) instead of always
    // filling chunks to _wordsPerChunk, so a trailing remainder doesn't leave the last chunk
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

    // Ties the revealed chunk/character count to playback progress rather than elapsed time,
    // so the text stays in sync even if the AudioSource is paused or its pitch changes.
    // Each chunk replaces the previous one on screen instead of stacking, so long
    // descriptions page through a few words at a time.
    IEnumerator RevealTextRoutine(AudioClip clip, List<string> chunks)
    {
        int lastChunkIndex = -1;

        while (_audioSource.isPlaying && _audioSource.clip == clip)
        {
            float progress = clip.length > 0f ? Mathf.Clamp01(_audioSource.time / clip.length) : 1f;
            lastChunkIndex = ShowChunkAtProgress(chunks, progress, lastChunkIndex);
            yield return null;
        }

        ShowChunkAtProgress(chunks, 1f, lastChunkIndex);
    }

    int ShowChunkAtProgress(List<string> chunks, float progress, int lastChunkIndex)
    {
        float chunkPosition = progress * chunks.Count;
        int chunkIndex = Mathf.Clamp(Mathf.FloorToInt(chunkPosition), 0, chunks.Count - 1);

        if (chunkIndex != lastChunkIndex) _text.text = chunks[chunkIndex];

        float chunkProgress = Mathf.Clamp01(chunkPosition - chunkIndex);
        _text.maxVisibleCharacters = Mathf.FloorToInt(chunkProgress * chunks[chunkIndex].Length);

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
