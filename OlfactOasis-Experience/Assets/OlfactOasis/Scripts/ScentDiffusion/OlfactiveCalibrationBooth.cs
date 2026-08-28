using System.Collections;
using System.Linq;
using UnityEngine;

public class OlfactiveCalibrationBooth : MonoBehaviour
{
    public PushButton PushButton;
    public ParticleSystem DiffusionVFX;

    public ScentData ScentData;

    [Header("Visibility")]
    [SerializeField] Renderer[] _renderers;
    [SerializeField] float _transparentAlpha = 0.15f;
    [SerializeField] float _fadeDuration = 0.5f;
    [SerializeField] AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    static readonly int s_baseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int s_colorID = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _propertyBlock;
    Color[] _baseColors;
    Coroutine _fadeCoroutine;
    float _currentAlpha = 1f;

    void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>()
                .Where(candidate => DiffusionVFX == null || candidate.gameObject != DiffusionVFX.gameObject)
                .ToArray();
        }

        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _baseColors[i] = GetRendererColor(_renderers[i]);
        }
    }

    public void Appear() => FadeTo(1f);

    public void Disappear() => FadeTo(_transparentAlpha);

    void FadeTo(float targetAlpha)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = _currentAlpha;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = _fadeCurve.Evaluate(Mathf.Clamp01(elapsed / _fadeDuration));
            SetAlpha(Mathf.LerpUnclamped(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
        _fadeCoroutine = null;
    }

    void SetAlpha(float alpha)
    {
        _currentAlpha = alpha;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer targetRenderer = _renderers[i];
            if (targetRenderer == null) continue;

            Color color = _baseColors[i];
            color.a = alpha;

            bool usesBaseColor = targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(s_baseColorID);

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(usesBaseColor ? s_baseColorID : s_colorID, color);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }

    Color GetRendererColor(Renderer targetRenderer)
    {
        Material material = targetRenderer.sharedMaterial;
        if (material == null) return Color.white;

        if (material.HasProperty(s_baseColorID)) return material.GetColor(s_baseColorID);
        if (material.HasProperty(s_colorID)) return material.GetColor(s_colorID);

        return Color.white;
    }
}
