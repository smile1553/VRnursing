using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DoorCloseTrigger : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] DoorSlideOpener doorOpener;

    [Header("Player Detection")]
    [SerializeField] Transform playerRoot;
    [SerializeField] Transform playerHead;
    [SerializeField] bool closeOnlyOnce = true;
    [SerializeField] bool checkPlayerPosition = true;
    [SerializeField] bool logEvents;

    [Header("Events")]
    [SerializeField] UnityEvent onDoorClosed;

    Collider triggerZone;
    bool closed;

    void Awake()
    {
        triggerZone = GetComponent<Collider>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;

        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;
    }

    void Reset()
    {
        var collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }

    void Update()
    {
        if (!checkPlayerPosition || triggerZone == null)
            return;

        if (closeOnlyOnce && closed)
            return;

        Transform target = playerHead != null ? playerHead : playerRoot;
        if (target == null)
            return;

        if (triggerZone.bounds.Contains(target.position))
            CloseDoor();
    }

    void OnTriggerEnter(Collider other)
    {
        if (closeOnlyOnce && closed)
            return;

        if (!IsPlayer(other.transform))
            return;

        CloseDoor();
    }

    void CloseDoor()
    {
        if (doorOpener == null)
            return;

        doorOpener.Close();
        closed = true;
        onDoorClosed?.Invoke();

        if (logEvents)
            Debug.Log("[DoorCloseTrigger] Door closed by player entering trigger.", this);
    }

    bool IsPlayer(Transform hit)
    {
        if (hit == null)
            return false;

        if (playerRoot != null && (hit == playerRoot || hit.IsChildOf(playerRoot)))
            return true;

        if (hit.CompareTag("Player"))
            return true;

        var current = hit;
        while (current != null)
        {
            if (current.name == "XR_Origin_Pure" || current.name == "XR Origin" || current.name == "Main Camera")
                return true;

            current = current.parent;
        }

        return false;
    }
}
