using System.Collections.Generic;
using UnityEngine;

public static class QuizPanelRuntimeHelper
{
    private static readonly Vector3 QuizLocalOffset = new Vector3(0f, 0.00f, 1.05f);
    private static readonly Vector3 QuizLocalEulerAngles = Vector3.zero;
    private static readonly List<GameObject> HiddenRecordObjects = new List<GameObject>();

    public static void BeginQuiz(GameObject quizRootOrPanel, string panelChildName)
    {
        WorldSpaceUiPlacer.PlaceCanvasInFrontOfCamera(quizRootOrPanel, QuizLocalOffset, QuizLocalEulerAngles);
        HideMedicalRecordButton();
    }

    public static void EndQuiz()
    {
        RestoreMedicalRecordButton();
    }

    private static void HideMedicalRecordButton()
    {
        if (HiddenRecordObjects.Count > 0)
            return;

        HUDManager[] managers = Object.FindObjectsOfType<HUDManager>(true);
        foreach (HUDManager manager in managers)
        {
            if (manager == null)
                continue;

            AddAndHide(manager.recordButtonUI != null ? manager.recordButtonUI.gameObject : null);
        }

        MedicalRecordHudButton[] buttons = Object.FindObjectsOfType<MedicalRecordHudButton>(true);
        foreach (MedicalRecordHudButton button in buttons)
            AddAndHide(button != null ? button.gameObject : null);

        AddAndHide(FindSceneObjectByName("MedicalRecord_HUD_Button"));
        AddAndHide(FindSceneObjectByName("MedicalRecord_Button"));
        AddAndHide(FindSceneObjectByName("MedicalRecordButton"));
        AddAndHide(FindSceneObjectByName("MedicalRecordHudButton"));
        AddAndHide(FindSceneObjectByName("RecordButton"));
        AddAndHide(FindSceneObjectByName("Record_Button"));
    }

    private static void RestoreMedicalRecordButton()
    {
        for (int i = 0; i < HiddenRecordObjects.Count; i++)
        {
            GameObject target = HiddenRecordObjects[i];
            if (target != null)
                target.SetActive(true);
        }

        HiddenRecordObjects.Clear();
    }

    private static void AddAndHide(GameObject target)
    {
        if (target == null || !target.activeSelf || HiddenRecordObjects.Contains(target))
            return;

        HiddenRecordObjects.Add(target);
        target.SetActive(false);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
            return activeObject;

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }
}
