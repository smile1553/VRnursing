using UnityEngine;

// Trigger callbacks require isTrigger and at least one Rigidbody on either this object or the entering object.
[RequireComponent(typeof(Collider))]
public class VRZoneTrigger : MonoBehaviour
{
    public string zoneId;

    [SerializeField] private VRInputRouter router;
    [SerializeField] private string hand = "Right";
    [SerializeField] private string source = "VR";

    private void Awake()
    {
        if (router == null)
        {
            router = FindObjectOfType<VRInputRouter>();
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (router == null)
        {
            return;
        }

        string id = string.IsNullOrEmpty(zoneId) ? gameObject.name : zoneId;
        router.EmitZoneEnter(id, hand, source);
    }

    private void OnTriggerExit(Collider other)
    {
        if (router == null)
        {
            return;
        }

        string id = string.IsNullOrEmpty(zoneId) ? gameObject.name : zoneId;
        router.EmitZoneExit(id, hand, source);
    }
}
