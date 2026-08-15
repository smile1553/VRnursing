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

        if (hidePanelOnStart && medicalRecordPanel != null)
            medicalRecordPanel.SetActive(false);
    }

    void LateUpdate()
    {
        if (!followHead || head == null)
            return;

        Vector3 targetPosition = head.TransformPoint(localOffset);
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
        if (medicalRecordPanel == null)
            return;

        if (medicalRecordPanel.activeSelf)
            HideMedicalRecord();
        else
            ShowMedicalRecord();
    }

    public void ShowMedicalRecord()
    {
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
