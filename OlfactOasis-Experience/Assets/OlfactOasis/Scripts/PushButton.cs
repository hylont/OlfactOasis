using EditorAttributes;
using System.Collections.Generic;
using UnityEngine;

public interface IButtonListener
{
    public void OnButtonDown();
    public void OnButtonUp();
}

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider))]
public class PushButton : MonoBehaviour
{
    [SerializeField] GameObject _pushablePart;
    [SerializeField] float _pushDistance = 0.015f;
    Vector3 _initialPushablePosition;
    [Header("Audio feedback")]
    [SerializeField] AudioClip _buttonDownClip;
    [SerializeField] AudioClip _buttonUpClip;
    AudioSource _feedbackSource;
    List<IButtonListener> _listeners = new();

    private void Start()
    {
        _feedbackSource = GetComponent<AudioSource>();

        _initialPushablePosition = _pushablePart.transform.localPosition;
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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("FingerTip"))
        {
            OnButtonDown();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("FingerTip"))
        {
            OnButtonUp();
        }
    }

    [Button("Button Down")]
    void OnButtonDown()
    {
        foreach(IButtonListener listener in _listeners)
        {
            listener.OnButtonDown();
        }
        
        _pushablePart.transform.localPosition = new Vector3(_initialPushablePosition.x, _initialPushablePosition.y - _pushDistance, _initialPushablePosition.z);

        if (_buttonDownClip != null)
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

        _pushablePart.transform.localPosition = _initialPushablePosition;

        if (_buttonUpClip != null)
        {
            _feedbackSource.clip = _buttonUpClip;
            _feedbackSource.Play();
        }
    }
}
