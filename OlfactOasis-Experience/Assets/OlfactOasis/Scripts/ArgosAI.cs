using EditorAttributes;
using Packages.com.lohan.unity_utils.Runtime.Scripts.AI;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using System.Collections;
using UnityEngine;

public enum EArgosMood
{
    DEFAULT, HAPPY
}

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

public class ArgosAI : MovementAI
{
    [Header("Argos config")]
    [SerializeField] bool _powerOnAtStart = false;
    [SerializeField] string _lookingAnim = "LOOKING_AT";
    [SerializeField] string _poweredAnim = "POWERED";

    [Header("Argos face")]
    [SerializeField] string _HappyBSName = "Happy";
    [SerializeField] string _BlinkingBSName = "Blinking";
    [SerializeField] string _TalkingBSName = "Talking";
    [SerializeField] AnimationCurve _moodTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] float _moodTransitionDuration = 0.4f;

    [Header("Random blink")]
    [SerializeField] float _blinkMinInterval = 2f;
    [SerializeField] float _blinkMaxInterval = 6f;
    [SerializeField] float _blinkDuration = 0.12f;
    [SerializeField] AnimationCurve _blinkCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    [Header("Look at")]
    [SerializeField] string _pivotingAnim = "PIVOTING";
    [SerializeField] float _pivotTurnSpeed = 180f;
    [SerializeField] float _lookAtAngleThreshold = 2f;

    [Header("Talking")]
    public AudioSource TalkingAudioSource;
    public AudioDescriptionCanvas AudioDescription;
    [SerializeField] int _talkingSampleWindow = 256;
    [SerializeField] float _talkingSensitivity = 12f;
    [SerializeField] float _talkingSmoothSpeed = 15f;
    [SerializeField] float _mouthScaleAmount = 0.5f;

    public Transform LeftPupil;
    public Transform RightPupil;
    public SkinnedMeshRenderer LeftEye;
    public SkinnedMeshRenderer RightEye;
    public SkinnedMeshRenderer Mouth;

    [SerializeField] SerializableDictionaryBase<EArgosMood, ArgosMoodData> _facesConfig;

    int _leftHappyBSIndex = -1;
    int _leftBlinkBSIndex = -1;
    int _rightHappyBSIndex = -1;
    int _rightBlinkBSIndex = -1;

    Vector3 _leftPupilRestPosition;
    Vector3 _rightPupilRestPosition;

    readonly ArgosMoodData _currentFace = new ArgosMoodData();
    Coroutine _moodTransitionCoroutine;
    ArgosMoodData _currentMoodData;
    float _blinkOverlay;

    Coroutine _lookAtCoroutine;
    bool _isPivoting;
    public bool IsPivoting => _isPivoting;

    int _talkingBSIndex = -1;
    float[] _talkingSamples;
    float _talkingLevel;
    Vector3 _mouthRestScale = Vector3.one;

    void Start()
    {
        _leftHappyBSIndex = ResolveBlendShapeIndex(LeftEye, _HappyBSName);
        _leftBlinkBSIndex = ResolveBlendShapeIndex(LeftEye, _BlinkingBSName);
        _rightHappyBSIndex = ResolveBlendShapeIndex(RightEye, _HappyBSName);
        _rightBlinkBSIndex = ResolveBlendShapeIndex(RightEye, _BlinkingBSName);
        _talkingBSIndex = ResolveBlendShapeIndex(Mouth, _TalkingBSName);

        if (LeftPupil != null) _leftPupilRestPosition = LeftPupil.transform.localPosition;
        if (RightPupil != null) _rightPupilRestPosition = RightPupil.transform.localPosition;
        if (Mouth != null) _mouthRestScale = Mouth.transform.localScale;
        
        if (AudioDescription != null) AudioDescription.Hide();

        _talkingSamples = new float[_talkingSampleWindow];

        if (_powerOnAtStart) PowerOn();

        SetMood(EArgosMood.DEFAULT);

        StartCoroutine(RandomBlinkRoutine());

    }

    [Button("Power On")]
    void PowerOn()
    {
        if (Animator == null)
        {
            LLogger.E("No animator set to argos, can't power on !");
            return;
        }

        Animator.SetBool(_poweredAnim, true);

        Talk("argos.introduction.presentation");
    }

    public void Talk(string p_clipID)
    {
        if(TalkingAudioSource == null)
        {
            LLogger.E("No audio source, Argos can't talk !");
            return;
        }

        if(AudioDescription == null)
        {
            LLogger.E("No audio description target set !");
            return;
        }

        AudioDescription.Show(ClipsManager.GetClip(p_clipID));
        
    }

    int ResolveBlendShapeIndex(SkinnedMeshRenderer p_renderer, string p_blendShapeName)
    {
        if (p_renderer == null || p_renderer.sharedMesh == null) return -1;

        int index = p_renderer.sharedMesh.GetBlendShapeIndex(p_blendShapeName);
        if (index < 0) LLog.W($"ArgosAI: blend shape '{p_blendShapeName}' not found on mesh '{p_renderer.sharedMesh.name}' ({p_renderer.name})");

        return index;
    }

    [Button("Set Mood to Happy")]
    void SetMoodHappy() => SetMood(EArgosMood.HAPPY);

    [Button("Set Mood to Default")]
    void SetMoodDefault() => SetMood(EArgosMood.DEFAULT);

    public void SetMood(EArgosMood p_mood)
    {
        if (!_facesConfig.TryGetValue(p_mood, out ArgosMoodData targetFace))
        {
            LLog.W($"ArgosAI: no face data configured for mood {p_mood}");
            return;
        }

        _currentMoodData = targetFace;

        if (_moodTransitionCoroutine != null) StopCoroutine(_moodTransitionCoroutine);
        _moodTransitionCoroutine = StartCoroutine(TransitionMoodRoutine(targetFace));
    }

    [Button("Look at Camera")]
    void LookAtCamera() => LookAt(Camera.main.transform);

    public void LookAt(Transform p_target)
    {
        if (_lookAtCoroutine != null) StopCoroutine(_lookAtCoroutine);
        _lookAtCoroutine = StartCoroutine(LookAtRoutine(p_target));
    }

    public void StopLookAt()
    {
        if (_lookAtCoroutine != null)
        {
            StopCoroutine(_lookAtCoroutine);
            _lookAtCoroutine = null;
        }

        SetPivoting(false);
        if (Animator != null) Animator.SetBool(_lookingAnim, false);
    }

    IEnumerator LookAtRoutine(Transform p_target)
    {
        if (Animator != null) Animator.SetBool(_lookingAnim, false);

        SetPivoting(true);

        while (p_target != null)
        {
            Vector3 direction = p_target.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f) break;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            if (Quaternion.Angle(transform.rotation, targetRotation) <= _lookAtAngleThreshold) break;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _pivotTurnSpeed * Time.deltaTime);
            yield return null;
        }

        SetPivoting(false);

        if (p_target != null && Animator != null) Animator.SetBool(_lookingAnim, true);

        _lookAtCoroutine = null;
    }

    void SetPivoting(bool p_pivoting)
    {
        _isPivoting = p_pivoting;
        if (Animator != null) Animator.SetBool(_pivotingAnim, p_pivoting);
    }

    IEnumerator RandomBlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(_blinkMinInterval, _blinkMaxInterval));

            if (_currentMoodData != null && _currentMoodData.AllowBlink)
            {
                yield return BlinkOnceRoutine();
            }
        }
    }

    IEnumerator BlinkOnceRoutine()
    {
        float elapsed = 0f;
        while (elapsed < _blinkDuration)
        {
            elapsed += Time.deltaTime;
            _blinkOverlay = _blinkCurve.Evaluate(Mathf.Clamp01(elapsed / _blinkDuration));
            yield return null;
        }

        _blinkOverlay = 0f;
    }

    IEnumerator TransitionMoodRoutine(ArgosMoodData p_target)
    {
        ArgosMoodData startFace = ArgosMoodData.Lerp(_currentFace, p_target, 0f);

        float elapsed = 0f;
        while (elapsed < _moodTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = _moodTransitionCurve.Evaluate(Mathf.Clamp01(elapsed / _moodTransitionDuration));
            SetCurrentFace(ArgosMoodData.Lerp(startFace, p_target, t));
            yield return null;
        }

        SetCurrentFace(p_target);
        _moodTransitionCoroutine = null;
    }

    void SetCurrentFace(ArgosMoodData p_face)
    {
        _currentFace.LEyeHappy = p_face.LEyeHappy;
        _currentFace.LEyeBlink = p_face.LEyeBlink;
        _currentFace.LPupilPositionOffset = p_face.LPupilPositionOffset;
        _currentFace.REyeHappy = p_face.REyeHappy;
        _currentFace.REyeBlink = p_face.REyeBlink;
        _currentFace.RPupilPositionOffset = p_face.RPupilPositionOffset;
    }

    // Re-applied every frame after the Animator's evaluation: idle/powered clips
    // imported from the FBX carry baked blend shape curves (usually at 0), which
    // would otherwise stomp our SetBlendShapeWeight calls made during Update.
    void LateUpdate()
    {
        ApplyFace(_currentFace);
        UpdateTalking();
    }

    void UpdateTalking()
    {
        if (TalkingAudioSource == null) return;
        
        float targetLevel = 0f;
            
        if(TalkingAudioSource.isPlaying)
        {
            TalkingAudioSource.GetOutputData(_talkingSamples, 0);

            float sum = 0f;
            for (int i = 0; i < _talkingSamples.Length; i++) sum += _talkingSamples[i] * _talkingSamples[i];

            float rms = Mathf.Sqrt(sum / _talkingSamples.Length);
            targetLevel = Mathf.Clamp01(rms * _talkingSensitivity);

            _talkingLevel = Mathf.Lerp(_talkingLevel, targetLevel, Time.deltaTime * _talkingSmoothSpeed);

            if (Mouth != null)
            {
                if (_talkingBSIndex >= 0) Mouth.SetBlendShapeWeight(_talkingBSIndex, 100f);
                Mouth.transform.localScale = _mouthRestScale * (1f + _talkingLevel * _mouthScaleAmount);
            }
        }
        else if (Mouth != null) Mouth.SetBlendShapeWeight(_talkingBSIndex, 0);        
    }

    void ApplyFace(ArgosMoodData p_face)
    {
        float leftBlink = Mathf.Max(p_face.LEyeBlink, _blinkOverlay);
        float rightBlink = Mathf.Max(p_face.REyeBlink, _blinkOverlay);

        if (LeftEye != null)
        {
            if (_leftHappyBSIndex >= 0) LeftEye.SetBlendShapeWeight(_leftHappyBSIndex, p_face.LEyeHappy * 100f);
            if (_leftBlinkBSIndex >= 0) LeftEye.SetBlendShapeWeight(_leftBlinkBSIndex, leftBlink * 100f);
        }

        if (RightEye != null)
        {
            if (_rightHappyBSIndex >= 0) RightEye.SetBlendShapeWeight(_rightHappyBSIndex, p_face.REyeHappy * 100f);
            if (_rightBlinkBSIndex >= 0) RightEye.SetBlendShapeWeight(_rightBlinkBSIndex, rightBlink * 100f);
        }

        if (LeftPupil != null)
        {
            LeftPupil.transform.localPosition = _leftPupilRestPosition + new Vector3(p_face.LPupilPositionOffset.x, p_face.LPupilPositionOffset.y, 0f);
        }

        if (RightPupil != null)
        {
            RightPupil.transform.localPosition = _rightPupilRestPosition + new Vector3(p_face.RPupilPositionOffset.x, p_face.RPupilPositionOffset.y, 0f);
        }
    }
}
