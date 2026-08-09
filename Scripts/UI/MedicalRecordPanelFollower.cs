using UnityEngine;

[DisallowMultipleComponent]
public class MedicalRecordPanelFollower : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] Transform head;
    [SerializeField] Vector3 localOffset = new Vector3(0f, -0.02f, 1.2f);
    [SerializeField] Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] bool placeOnEnable = true;

    void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    void OnEnable()
    {
        if (placeOnEnable)
            PlaceInFrontOfHead();
    }

    public void PlaceInFrontOfHead()
    {
        if (head == null)
            return;

        transform.position = head.TransformPoint(localOffset);
        transform.rotation = head.rotation * Quaternion.Euler(localEulerAngles);
    }
}
