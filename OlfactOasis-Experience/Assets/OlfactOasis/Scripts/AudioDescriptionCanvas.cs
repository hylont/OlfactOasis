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

    public bool IsShowing { get; private set; }

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        SetVisibility(0f, Vector3.zero);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    public void Show(AudioData p_audioData)
    {
        if (p_audioData == null || p_audioData.Clip == null)
        {
            LLogger.E("AudioDescriptionCanvas: AudioData or its Clip is missing");
            return;
        }

        if (_audioSource == null)
        {
            LLogger.E("AudioDescriptionCanvas: no AudioSource assigned to observe");
            return;
        }

        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(ShowRoutine(p_audioData));
    }

    public void Hide()
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        IsShowing = false;
        _activeCoroutine = StartCoroutine(AnimateVisibilityRoutine(false));
    }

    IEnumerator ShowRoutine(AudioData p_audioData)
    {
        IsShowing = true;

        _audioSource.clip = p_audioData.Clip;
        _audioSource.Play();

        string description = p_audioData.asText ?? string.Empty;
        List<string> chunks = SplitIntoWordChunks(description, _wordsPerChunk);
        _text.text = chunks[0];
        _text.maxVisibleCharacters = 0;

        yield return AnimateVisibilityRoutine(true);
        yield return RevealTextRoutine(p_audioData.Clip, chunks);

        yield return new WaitForSeconds(_hideDelay);

        yield return AnimateVisibilityRoutine(false);

        IsShowing = false;
        _activeCoroutine = null;
    }

    static List<string> SplitIntoWordChunks(string p_text, int p_wordsPerChunk)
    {
        string[] words = p_text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        List<string> chunks = new List<string>();

        if (words.Length == 0)
        {
            chunks.Add(string.Empty);
            return chunks;
        }

        for (int i = 0; i < words.Length; i += p_wordsPerChunk)
        {
            int count = Mathf.Min(p_wordsPerChunk, words.Length - i);
            chunks.Add(string.Join(" ", words, i, count));
        }

        return chunks;
    }

    // Ties the revealed chunk/character count to playback progress rather than elapsed time,
    // so the text stays in sync even if the AudioSource is paused or its pitch changes.
    // Each chunk replaces the previous one on screen instead of stacking, so long
    // descriptions page through a few words at a time.
    IEnumerator RevealTextRoutine(AudioClip p_clip, List<string> p_chunks)
    {
        int lastChunkIndex = -1;

        while (_audioSource.isPlaying && _audioSource.clip == p_clip)
        {
            float progress = p_clip.length > 0f ? Mathf.Clamp01(_audioSource.time / p_clip.length) : 1f;
            lastChunkIndex = ShowChunkAtProgress(p_chunks, progress, lastChunkIndex);
            yield return null;
        }

        ShowChunkAtProgress(p_chunks, 1f, lastChunkIndex);
    }

    int ShowChunkAtProgress(List<string> p_chunks, float p_progress, int p_lastChunkIndex)
    {
        float chunkPosition = p_progress * p_chunks.Count;
        int chunkIndex = Mathf.Clamp(Mathf.FloorToInt(chunkPosition), 0, p_chunks.Count - 1);

        if (chunkIndex != p_lastChunkIndex) _text.text = p_chunks[chunkIndex];

        float chunkProgress = Mathf.Clamp01(chunkPosition - chunkIndex);
        _text.maxVisibleCharacters = Mathf.FloorToInt(chunkProgress * p_chunks[chunkIndex].Length);

        return chunkIndex;
    }

    IEnumerator AnimateVisibilityRoutine(bool p_show)
    {
        float startAlpha = _canvasGroup.alpha;
        Vector3 startScale = _rectTransform.localScale;
        float targetAlpha = p_show ? 1f : 0f;
        Vector3 targetScale = p_show ? _shownScale : Vector3.zero;
        float duration = p_show ? _appearDuration : _disappearDuration;
        AnimationCurve curve = p_show ? _appearCurve : _disappearCurve;

        _canvasGroup.blocksRaycasts = p_show;
        _canvasGroup.interactable = p_show;

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

    void SetVisibility(float p_alpha, Vector3 p_scale)
    {
        _canvasGroup.alpha = p_alpha;
        _rectTransform.localScale = p_scale;
    }
}
