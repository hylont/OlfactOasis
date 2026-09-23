using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Shows a loading text/subtext until Olfy's connection is confirmed (OlfyHandler.OnOlfyReady, itself
// fed by OlfyManager.isReady), then fades it out (reusing ScreenFader, no duplicated fade logic) and
// hands off to whatever should start the experience next (wired via OnLoadingComplete).
// IMPORTANT (Editor setup) : leave ScreenFader's "Fade Out At Start" UNCHECKED, so it only fades once
// this script tells it to (once Olfy is actually ready) instead of immediately on scene load.
public class LoadingScreenController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] OlfyHandler _olfyHandler;
    [SerializeField] ScreenFader _screenFader;
    [SerializeField] GameObject _loadingCanvas;

    public UnityEvent OnLoadingComplete;

    void Start()
    {
        if (_olfyHandler == null) _olfyHandler = OlfyHandler.Instance;

        if (_olfyHandler == null || _screenFader == null || _loadingCanvas == null)
        {
            LLogger.E("LoadingScreenController: missing a required dependency (OlfyHandler, ScreenFader or loading canvas).");
            return;
        }

        _olfyHandler.OnOlfyReady.AddListener(OnOlfyReady);
    }

    void OnDestroy()
    {
        if (_olfyHandler != null) _olfyHandler.OnOlfyReady.RemoveListener(OnOlfyReady);
    }

    void OnOlfyReady()
    {
        StartCoroutine(HideLoadingScreenRoutine());
    }

    IEnumerator HideLoadingScreenRoutine()
    {
        _screenFader.FadeToHidden();
        yield return new WaitForSeconds(_screenFader.FadeDuration);

        _loadingCanvas.SetActive(false);
        OnLoadingComplete?.Invoke();
    }
}
