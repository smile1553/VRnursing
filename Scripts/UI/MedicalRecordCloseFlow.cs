using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class MedicalRecordCloseFlow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject medicalRecordPanel;
    [SerializeField] GameObject medicalRecordHudCanvas;
    [SerializeField] DoorSlideOpener doorOpener;
    [SerializeField] PediatricVitalSignsPart0Flow part0Flow;

    [Header("Options")]
    [SerializeField] bool showHudAfterFirstClose = true;
    [SerializeField] bool openDoorOnlyOnce = true;
    [SerializeField] bool startPart0BeforeOpeningDoor = true;

    [Header("Events")]
    [SerializeField] UnityEvent onFirstDoorOpened;

    bool doorOpened;
    bool doorSequenceStarted;

    public void CloseMedicalRecord()
    {
        if (medicalRecordPanel != null)
            medicalRecordPanel.SetActive(false);

        bool shouldShowHud = doorOpened || showHudAfterFirstClose;
        if (shouldShowHud && medicalRecordHudCanvas != null)
            medicalRecordHudCanvas.SetActive(true);

        if (startPart0BeforeOpeningDoor && !doorOpened)
        {
            if (doorSequenceStarted)
                return;

            if (TryStartPart0())
                return;
        }

        OpenDoorAfterPart0();
    }

    public void OpenDoorAfterPart0()
    {
        if (doorOpener == null)
            return;

        if (openDoorOnlyOnce && doorOpened)
            return;

        doorSequenceStarted = true;
        doorOpener.Open();
        doorOpened = true;
        onFirstDoorOpened?.Invoke();
    }

    bool TryStartPart0()
    {
        if (part0Flow == null)
            part0Flow = FindObjectOfType<PediatricVitalSignsPart0Flow>(true);

        if (part0Flow == null)
        {
            Debug.LogWarning("[MedicalRecordCloseFlow] Part0 flow is missing, opening the door immediately.", this);
            return false;
        }

        doorSequenceStarted = true;
        part0Flow.StartPart0(this);
        return true;
    }
}
