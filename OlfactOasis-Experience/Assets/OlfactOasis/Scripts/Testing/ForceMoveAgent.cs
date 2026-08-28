using Packages.com.lohan.unity_utils.Runtime.Scripts.AI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ForceMoveAgent : MonoBehaviour
{
    //TODO ADD IN THE PACKAGE

    [Header("Agent")]
    [SerializeField] private MovementAI _agent;

    [Header("Interaction")]
    [SerializeField] private InputAction _moveAction;

    [Header("Target")]
    [SerializeField] private string _targetTag = "WALKABLE";

    [Header("Callbacks")]
    public UnityEvent OnDestinationSet;

    void Start()
    {
        if(_agent == null)
        {
            _agent = FindAnyObjectByType<MovementAI>();
        }
    }

    void OnEnable()
    {
        _moveAction.Enable();
    }

    void OnDisable()
    {
        _moveAction.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        if (_moveAction.WasPerformedThisFrame())
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if(hit.collider.gameObject.CompareTag(_targetTag))
                {
                    OnDestinationSet?.Invoke();
                    _agent.SetDestination(hit.point);
                }
                else
                {
                    Debug.Log("[CLICK MOVEMENT] Hit detected but not on target tag: " + hit.collider.gameObject.name);
                }
            }
            else
            {
                Debug.Log("[CLICK MOVEMENT] No hit detected");
            }
        }
    }
}
