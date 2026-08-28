using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SmoothSceneSwitch : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup m_fadeCanvasGroup;
    [SerializeField] private float m_fadeDuration = 1f;

    private bool m_isSwitching;

    void Awake()
    {
        if (m_fadeCanvasGroup == null)
        {
            m_fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
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
        if (m_isSwitching)
        {
            return;
        }

        StartCoroutine(SwitchSceneRoutine(loadScene));
    }

    private IEnumerator SwitchSceneRoutine(Func<AsyncOperation> loadScene)
    {
        m_isSwitching = true;

        // Fade to black before unloading the current scene.
        yield return Fade(0f, 1f);

        AsyncOperation loadOperation = loadScene();
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        // Fade back in once the new scene has finished loading.
        yield return Fade(1f, 0f);

        m_isSwitching = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (m_fadeCanvasGroup == null)
        {
            yield break;
        }

        m_fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < m_fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / m_fadeDuration));
            yield return null;
        }

        SetAlpha(to);
        m_fadeCanvasGroup.blocksRaycasts = to > 0f;
    }

    private void SetAlpha(float alpha)
    {
        if (m_fadeCanvasGroup != null)
        {
            m_fadeCanvasGroup.alpha = alpha;
        }
    }
}
