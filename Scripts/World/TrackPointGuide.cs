using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TrackPointGuide : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform playerHead;
    [SerializeField] GameObject markerRoot;

    [Header("Arrival")]
    [SerializeField] float arriveRadius = 0.8f;
    [SerializeField] bool ignoreHeight = true;
    [SerializeField] bool hideOnArrive = true;
    [SerializeField] bool triggerOnlyOnce = true;

    [Header("Events")]
    [SerializeField] UnityEvent onArrived;

    bool active;
    bool arrived;

    void Awake()
    {
        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;

        if (markerRoot == null)
            markerRoot = gameObject;

        Hide();
    }

    void Update()
    {
        if (!active || (triggerOnlyOnce && arrived) || playerHead == null)
            return;

        Vector3 target = transform.position;
        Vector3 player = playerHead.position;

        if (ignoreHeight)
        {
            target.y = 0f;
            player.y = 0f;
        }

        if (Vector3.Distance(player, target) <= arriveRadius)
            Arrive();
    }

    public void Show()
    {
        if (triggerOnlyOnce && arrived)
            return;

        active = true;
        if (markerRoot != null)
            markerRoot.SetActive(true);
    }

    public void Hide()
    {
        active = false;
        if (markerRoot != null)
            markerRoot.SetActive(false);
    }

    public void Arrive()
    {
        arrived = true;
        active = false;

        if (hideOnArrive && markerRoot != null)
            markerRoot.SetActive(false);

        onArrived?.Invoke();
    }
}
