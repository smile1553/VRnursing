using UnityEngine;

[DisallowMultipleComponent]
public class MedicalRecordHudButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform head;
    [SerializeField] GameObject medicalRecordPanel;

    [Header("HUD Placement")]
    [SerializeField] bool followHead = true;
    [SerializeField] Vector3 localOffset = new Vector3(0f, 0.32f, 1.4f);
    [SerializeField] float followLerp = 12f;
    [Tooltip("Extra offset added to Local Offset (x < 0 = more to the left).")]
    [SerializeField] Vector3 extraLocalOffset = new Vector3(-0.06f, 0f, 0f);
    [Tooltip("When the button is fixed to the camera (Follow Head off): metres to move it up (+) / down (-).")]
    [SerializeField] float fixedRaise = 0.055f;

    [Header("Panel")]
    [SerializeField] bool hidePanelOnStart = true;
    [SerializeField] bool hideHudWhenPanelOpens = true;
    [SerializeField] GameObject hudRoot;

    void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        ResolveMedicalRecordPanel();

        // Fixed to the camera: nudge it up a little so the bear sticker fits right below it.
        if (!followHead && head != null && transform.IsChildOf(head))
            transform.position += head.up * fixedRaise;

        if (hidePanelOnStart && medicalRecordPanel != null)
            medicalRecordPanel.SetActive(false);
    }

    void ResolveMedicalRecordPanel()
    {
        if (medicalRecordPanel != null)
            return;

        var byName = GameObject.Find("MedicalRecordPanel");
        if (byName == null)
            byName = GameObject.Find("MedicalRecord_HUD_Canvas");
        if (byName == null)
            byName = GameObject.Find("MedicalRecordCanvas");

        medicalRecordPanel = byName;
    }

    void LateUpdate()
    {
        if (!followHead || head == null)
            return;

        Vector3 targetPosition = head.TransformPoint(localOffset + extraLocalOffset);
        Quaternion targetRotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);

        if (followLerp <= 0f)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followLerp);
        }
    }

    public void ToggleMedicalRecord()
    {
        ResolveMedicalRecordPanel();
        if (medicalRecordPanel == null)
            return;

        if (medicalRecordPanel.activeSelf)
            HideMedicalRecord();
        else
            ShowMedicalRecord();
    }

    public void ShowMedicalRecord()
    {
        ResolveMedicalRecordPanel();
        if (medicalRecordPanel == null)
            return;

        medicalRecordPanel.SetActive(true);

        if (hideHudWhenPanelOpens && hudRoot != null)
            hudRoot.SetActive(false);
    }

    public void HideMedicalRecord()
    {
        if (medicalRecordPanel != null)
            medicalRecordPanel.SetActive(false);
    }
}
