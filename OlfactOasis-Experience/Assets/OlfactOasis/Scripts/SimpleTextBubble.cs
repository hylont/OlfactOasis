using UnityEngine;

public class SimpleTextBubble : MonoBehaviour, IClipsReceiver
{
    [SerializeField] AudioDescriptionCanvas _audioDescriptionCanvas;
    public float DistanceToCamera = 1.0f;

    void Start()
    {
        if(_audioDescriptionCanvas == null)
        {
            LLogger.E("AudioDescriptionCanvas is not assigned !");
            return;
        }

        _audioDescriptionCanvas.Hide();
    }

    public void HandleClip(string clipID)
    {
        _audioDescriptionCanvas.Show(ClipsManager.GetClip(clipID));
    }

    public void StopClip(string clipID)
    {
        _audioDescriptionCanvas.Hide();
    }

    public void HandleClip(string clipID, Transform target)
    {
        if(target == null)
        {
            transform.position = Camera.main.transform.position + Camera.main.transform.forward * DistanceToCamera;
        }
        else
        {
            transform.position = target.position;
        }
        HandleClip(clipID);
    }
}
