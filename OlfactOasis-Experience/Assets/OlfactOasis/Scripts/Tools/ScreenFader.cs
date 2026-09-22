using EditorAttributes;
using System.Collections;
using UnityEngine;

// Fades a full-screen CanvasGroup (a black Image parented to the HMD camera) to opaque.
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    public float FadeDuration = 2f;

    private void Start()
    {
        if (_fadeCanvasGroup == null)
        {
            LLogger.E("ScreenFader has no CanvasGroup assigned.");
            return;
        }

        _fadeCanvasGroup.alpha = 1f;
        FadeToHidden();
    }

    [Button("Fade To Hidden")]
    public void FadeToHidden()
    {
        StartCoroutine(FadeCoroutine(_fadeCanvasGroup.alpha, 0f));
    }

    [Button("Fade in and out")]
    public void FadeInAndOut()
    {
        StartCoroutine(FadeInAndOutCoroutine());
    }

    [Button("Fade To Black")]
    public void FadeToBlack()
    {
        StartCoroutine(FadeCoroutine(_fadeCanvasGroup.alpha, 1f));
    }


    IEnumerator FadeInAndOutCoroutine()
    {
        FadeToBlack();
        yield return new WaitForSeconds(FadeDuration);
        FadeToHidden();
    }

    private IEnumerator FadeCoroutine(float start, float end)
    {
        float elapsed = 0f;

        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeCanvasGroup.alpha = Mathf.Lerp(start, end, elapsed / FadeDuration);
            yield return null;
        }

        _fadeCanvasGroup.alpha = end;
    }
}
