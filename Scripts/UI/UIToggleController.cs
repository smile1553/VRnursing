using UnityEngine;

public class UIToggleController : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] private GameObject medicalRecordUI;
    [SerializeField] private GameObject dialogueUI;
    [SerializeField] private GameObject quizUI;

    private void Awake()
    {
        ResolveMedicalRecordUi();
    }

    private void ResolveMedicalRecordUi()
    {
        if (medicalRecordUI != null)
            return;

        var byName = GameObject.Find("MedicalRecordPanel");
        if (byName == null)
            byName = GameObject.Find("MedicalRecord_HUD_Canvas");
        if (byName == null)
            byName = GameObject.Find("MedicalRecordCanvas");

        medicalRecordUI = byName;
    }

    public void ToggleMedicalRecord()
    {
        ResolveMedicalRecordUi();

        if (medicalRecordUI == null)
        {
            RuntimeLog.Warning("[UIToggleController] ToggleMedicalRecord skipped: medicalRecordUI is not assigned.");
            return;
        }

        if (medicalRecordUI.activeSelf)
            HideMedicalRecord();
        else
            ShowMedicalRecord();
    }

    public void ShowMedicalRecord()
    {
        ResolveMedicalRecordUi();

        if (medicalRecordUI == null)
        {
            RuntimeLog.Warning("[UIToggleController] ShowMedicalRecord skipped: medicalRecordUI is not assigned.");
            return;
        }

        medicalRecordUI.SetActive(true);

        if (dialogueUI != null)
            dialogueUI.SetActive(false);
        else
            RuntimeLog.Warning("[UIToggleController] dialogueUI is not assigned.");

        if (quizUI != null)
            quizUI.SetActive(false);
        else
            RuntimeLog.Warning("[UIToggleController] quizUI is not assigned.");
    }

    public void HideMedicalRecord()
    {
        if (medicalRecordUI == null)
        {
            RuntimeLog.Warning("[UIToggleController] HideMedicalRecord skipped: medicalRecordUI is not assigned.");
            return;
        }

        medicalRecordUI.SetActive(false);
    }
}
