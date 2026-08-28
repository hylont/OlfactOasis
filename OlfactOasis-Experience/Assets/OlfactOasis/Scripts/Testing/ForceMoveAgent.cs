using Packages.com.lohan.unity_utils.Runtime.Scripts.AI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ForceMoveAgent : MonoBehaviour
{
    //TODO ADD IN THE PACKAGE

    [Header("Agent")]
    [SerializeField] private MovementAI m_agent;

    [Header("Interaction")]
    [SerializeField] private InputAction moveAction;

    [Header("Target")]
    [SerializeField] private string TargetTag = "WALKABLE";

    [Header("Callbacks")]
    public UnityEvent OnDestinationSet;

    void Start()
    {
        if(m_agent == null)
        {
            m_agent = FindAnyObjectByType<MovementAI>();
        }
    }

    void OnEnable()
    {
        moveAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        if (moveAction.WasPerformedThisFrame())
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if(hit.collider.gameObject.CompareTag(TargetTag))
                {
                    OnDestinationSet?.Invoke();
                    m_agent.SetDestination(hit.point);
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
