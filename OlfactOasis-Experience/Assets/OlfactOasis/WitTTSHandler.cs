using EditorAttributes;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum EDialogueState
{    NONE, ERROR, READY, THINKING, EXPRESSING, SPEAKING
}

public class WitTTSHandler : MonoBehaviour, IClipsReceiver
{
    [Header("Dependencies")]
    [SerializeField] TTSSpeaker _speaker;
    [Header("Optionnal, for ADC hooks")]
    [SerializeField] AudioDescriptionCanvas _audioDescriptionCanvas;
    string _lastAnswer = "";
    string _lastImportantAnswer = "";
    Queue<(string, bool)> _textsToSay = new();

    [SerializeField] EDialogueState _state = EDialogueState.NONE;


    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI _answerText;
    [SerializeField] int _wordsPerChunk = 6;
    [Tooltip("Streaming TTS clips don't reliably expose a playable AudioClip/AudioSource to sync the reveal against, so it's paced from this speaking-rate estimate instead.")]
    [SerializeField] float _wordsPerSecond = 2.5f;
    Coroutine _revealAnswerCoroutine;
    [Header("")]
    [SerializeField] string _readyText = "^_^";
    [SerializeField] string _thinkingText = "...";
    [SerializeField] string _expressingText = ":P";
    [SerializeField] string _offlineText = "X_X";


    [Header("Debug")]
    [SerializeField] bool _debug = false;
    [SerializeField] string _testClip = "Je suis fatigué, patron.";

    [Button("Test clip")]
    void TestClip()
    {
        SayText(_testClip);
    }

    #region STATES
    public void SetState(EDialogueState p_state)
    {
        if (p_state == _state) return;

        if (_debug) LLogger.L("New dialogue state : " + _state + " => " + p_state);

        _state = p_state;

        if(_answerText == null) return;

        _answerText.gameObject.SetActive(false);
        _answerText.text = "";

        switch (_state)
        {
            case EDialogueState.NONE:

                break;
            case EDialogueState.ERROR:
                _answerText.text = _offlineText;
                break;
            case EDialogueState.READY:
                _answerText.text = _readyText;
                break;
            case EDialogueState.THINKING:
                _answerText.text = _thinkingText;
                break;
            case EDialogueState.EXPRESSING:
                _answerText.text = _expressingText;
                break;
            case EDialogueState.SPEAKING:
                ActivateAnswerText();
                break;
        }
    }

    // Reveals _lastAnswer word-by-word, paced from an estimated speaking duration (see _wordsPerSecond) -
    // TTSSpeaker.AudioSource/TTSClipData.clip are only valid when the underlying player is a classic
    // UnityAudioPlayer, which isn't guaranteed for a streaming voice, so we don't rely on them here.
    private void ActivateAnswerText()
    {
        _answerText.gameObject.SetActive(true);

        if (_revealAnswerCoroutine != null) StopCoroutine(_revealAnswerCoroutine);

        float duration = AudioDescriptionCanvas.EstimateSpeechDuration(_lastAnswer, _wordsPerSecond);
        _revealAnswerCoroutine = StartCoroutine(AudioDescriptionCanvas.RevealChunkedTextTimed(_answerText, _lastAnswer, _wordsPerChunk, duration));
    }

    public void SetLastAnswer(string p_answer)
    {
        _lastAnswer = p_answer;
    }

    public EDialogueState GetState() { return _state; }

    #endregion STATES

    private void Start()
    {
        _speaker.Events.OnPlaybackStart.AddListener(OnPlaybackStart);
        _speaker.Events.OnAudioClipPlaybackFinished.AddListener(OnAudioClipPlaybackFinished);
        _speaker.Events.OnLoadFailed.AddListener(OnLoadFailed);
        _speaker.Events.OnLoadBegin.AddListener(OnLoadBegin);

        SetState(EDialogueState.NONE);
    }

    #region WIT
    private void OnLoadFailed(TTSSpeaker arg0, TTSClipData arg1, string arg2)
    {
        SetState(EDialogueState.ERROR);
        Debug.LogError("[SUN WIT] Load failed !");

        StartCoroutine(SayNextText_Delay());
    }

    private void OnLoadBegin(TTSSpeaker arg0, TTSClipData arg1)
    {
        SetState(EDialogueState.EXPRESSING);
        if(_audioDescriptionCanvas != null) _audioDescriptionCanvas.Show(new AudioData(null, "..."));
    }
    private void OnPlaybackStart(TTSSpeaker arg0, TTSClipData arg1)
    {
        SetState(EDialogueState.SPEAKING);

        if (_audioDescriptionCanvas == null) return;

        // arg1.clip is only non-null when the clip stream implements IAudioClipProvider, which a
        // streaming voice may not - fall back to the same estimated-duration reveal as _answerText.
        AudioData audioData = new AudioData(arg1.clip, _lastAnswer);
        if (arg1.clip == null) audioData.EstimatedSpeechDuration = AudioDescriptionCanvas.EstimateSpeechDuration(_lastAnswer, _wordsPerSecond);

        _audioDescriptionCanvas.Show(audioData);
    }

    private void OnAudioClipPlaybackFinished(AudioClip arg0)
    {
        SetState(EDialogueState.NONE);

        if(_audioDescriptionCanvas != null) _audioDescriptionCanvas.Hide();

        if (_textsToSay.Count > 0)
        {
            _textsToSay.Dequeue();
            SayNextText();
        }
    }

    /// <summary>
    /// Calls <see cref="SayNextText"/> after 5 seconds, due to occasionnal Wit forced delays
    /// </summary>
    /// <returns></returns>
    IEnumerator SayNextText_Delay()
    {
        yield return new WaitForSeconds(5);

        SayNextText();
    }

    /// <summary>
    /// Peeks at the first text to say, and tries to speak it.
    /// Due to occasionnal unstability, the tried text is set as the last answer.
    /// </summary>
    void SayNextText()
    {
        if (_textsToSay.Count == 0) return;

        (string, bool) nextText = _textsToSay.Peek();

        SetLastAnswer(nextText.Item1);

        if (nextText.Item2) _lastImportantAnswer = nextText.Item1;

        _speaker.Speak(nextText.Item1);
    }

    public void SayText(string p_text, bool p_isImportant = false)
    {
        _textsToSay.Enqueue((p_text, p_isImportant));

        if (_textsToSay.Count == 1)
        {
            SayNextText();
        }

    }

    public void Repeat()
    {
        if (string.IsNullOrEmpty(_lastImportantAnswer)) return;

        if (_state == EDialogueState.EXPRESSING || _state == EDialogueState.SPEAKING) return;

        SetState(EDialogueState.EXPRESSING);
        _lastAnswer = _lastImportantAnswer;
        _speaker.Speak(_lastImportantAnswer);
    }

    #endregion WIT

    private void OnApplicationQuit()
    {
        _speaker.Events.OnPlaybackStart.RemoveListener(OnPlaybackStart);
        _speaker.Events.OnAudioClipPlaybackFinished.RemoveListener(OnAudioClipPlaybackFinished);
        _speaker.Events.OnLoadFailed.RemoveListener(OnLoadFailed);
    }

    public void HandleClip(string clip)
    {
        SayText(clip);
    }

    public void HandleClip(string clip, Transform target)
    {
        SayText(clip);
        LLogger.W("HandleClip with target is not implemented yet.");
    }

    public void StopClip(string clip)
    {
        LLogger.W("Stopping of a specific clip is not implemented yet.");
        SayNextText();
    }
}