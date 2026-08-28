using EditorAttributes;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public interface IButtonListener
{
    public void OnButtonDown();
    public void OnButtonUp();
}

[RequireComponent(typeof(AudioSource))]
public class PushButton : MonoBehaviour
{
    [Header("Audio feedback")]
    [SerializeField] AudioClip _buttonDownClip;
    [SerializeField] AudioClip _buttonUpClip;
    AudioSource _feedbackSource;
    List<IButtonListener> _listeners = new();

    private void Start()
    {
        _feedbackSource = GetComponent<AudioSource>();
    }

    public void AddListener(IButtonListener listener)
    {
        if (_listeners.Contains(listener))
        {
            LLogger.W($"Observable {name} already has this listener.");
            return;
        }
        _listeners.Add(listener);
    }

    public void RemoveListener(IButtonListener listener)
    {
        _listeners.Remove(listener);
    }

    [Button("Button Down")]
    void OnButtonDown()
    {
        foreach(IButtonListener listener in _listeners)
        {
            listener.OnButtonDown();
        }

        if(_buttonDownClip != null)
        {
            _feedbackSource.clip = _buttonDownClip;
            _feedbackSource.Play();
        }
    }

    [Button("Button Up")]
    void OnButtonUp()
    {
        foreach(IButtonListener listener in _listeners)
        {
            listener.OnButtonUp();
        }

        if (_buttonUpClip != null)
        {
            _feedbackSource.clip = _buttonUpClip;
            _feedbackSource.Play();
        }
    }
}
