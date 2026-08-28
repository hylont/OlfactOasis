using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SmoothSceneSwitch : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    [SerializeField] private float _fadeDuration = 1f;

    private bool _isSwitching;

    void Awake()
    {
        if (_fadeCanvasGroup == null)
        {
            _fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        DontDestroyOnLoad(gameObject);
        SetAlpha(0f);
    }

    public void SwitchScene(string sceneName)
    {
        StartSwitch(() => SceneManager.LoadSceneAsync(sceneName));
    }

    public void SwitchScene(int sceneBuildIndex)
    {
        StartSwitch(() => SceneManager.LoadSceneAsync(sceneBuildIndex));
    }

    private void StartSwitch(Func<AsyncOperation> loadScene)
    {
        if (_isSwitching)
        {
            return;
        }

        StartCoroutine(SwitchSceneRoutine(loadScene));
    }

    private IEnumerator SwitchSceneRoutine(Func<AsyncOperation> loadScene)
    {
        _isSwitching = true;

        // Fade to black before unloading the current scene.
        yield return Fade(0f, 1f);

        AsyncOperation loadOperation = loadScene();
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        // Fade back in once the new scene has finished loading.
        yield return Fade(1f, 0f);

        _isSwitching = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCanvasGroup == null)
        {
            yield break;
        }

        _fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / _fadeDuration));
            yield return null;
        }

        SetAlpha(to);
        _fadeCanvasGroup.blocksRaycasts = to > 0f;
    }

    private void SetAlpha(float alpha)
    {
        if (_fadeCanvasGroup != null)
        {
            _fadeCanvasGroup.alpha = alpha;
        }
    }
}
