using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class MedicalRecordCloseFlow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject medicalRecordPanel;
    [SerializeField] GameObject medicalRecordHudCanvas;
    [SerializeField] DoorSlideOpener doorOpener;

    [Header("Options")]
    [SerializeField] bool showHudAfterFirstClose = true;
    [SerializeField] bool openDoorOnlyOnce = true;

    [Header("Events")]
    [SerializeField] UnityEvent onFirstDoorOpened;

    bool doorOpened;

    public void CloseMedicalRecord()
    {
        if (medicalRecordPanel != null)
            medicalRecordPanel.SetActive(false);

        bool shouldShowHud = doorOpened || showHudAfterFirstClose;
        if (shouldShowHud && medicalRecordHudCanvas != null)
            medicalRecordHudCanvas.SetActive(true);

        if (doorOpener == null)
            return;

        if (openDoorOnlyOnce && doorOpened)
            return;

        doorOpener.Open();
        doorOpened = true;
        onFirstDoorOpened?.Invoke();
    }
}
