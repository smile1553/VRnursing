using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LoginStudentIdController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TMP_InputField studentIdInput;
    [SerializeField] GameObject loginScreenRoot;
    [SerializeField] GameObject nextScreenRoot;

    [Header("Options")]
    [SerializeField] bool requireStudentId = true;
    [SerializeField] string studentIdPrefsKey = "StudentId";
    [SerializeField] string nextSceneName;
    [SerializeField] bool logLogin;

    [Header("Events")]
    [SerializeField] UnityEvent onLoginSucceeded;

    void Awake()
    {
        if (studentIdInput == null)
            studentIdInput = GetComponentInChildren<TMP_InputField>(true);

        if (loginScreenRoot == null)
            loginScreenRoot = gameObject;
    }

    void OnEnable()
    {
        if (studentIdInput != null)
            studentIdInput.onSubmit.AddListener(OnStudentIdSubmitted);
    }

    void OnDisable()
    {
        if (studentIdInput != null)
            studentIdInput.onSubmit.RemoveListener(OnStudentIdSubmitted);
    }

    public void Login()
    {
        string studentId = studentIdInput != null ? studentIdInput.text.Trim() : string.Empty;

        if (requireStudentId && string.IsNullOrEmpty(studentId))
        {
            Debug.LogWarning("[LoginStudentIdController] Student ID is required.", this);
            FocusStudentIdInput();
            return;
        }

        PlayerPrefs.SetString(studentIdPrefsKey, studentId);
        PlayerPrefs.Save();
        AssignStudentIdToScoreExporters(studentId);

        if (logLogin)
            Debug.Log($"[LoginStudentIdController] Login succeeded. studentId={studentId}", this);

        if (nextScreenRoot != null)
            nextScreenRoot.SetActive(true);

        if (loginScreenRoot != null)
            loginScreenRoot.SetActive(false);

        onLoginSucceeded?.Invoke();

        if (!string.IsNullOrWhiteSpace(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }

    void OnStudentIdSubmitted(string _)
    {
        Login();
    }

    void FocusStudentIdInput()
    {
        if (studentIdInput == null)
            return;

        studentIdInput.Select();
        studentIdInput.ActivateInputField();
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
    }

    static void AssignStudentIdToScoreExporters(string studentId)
    {
        var exporters = FindObjectsOfType<MonoBehaviour>();
        foreach (var exporter in exporters)
        {
            if (exporter == null || exporter.GetType().Name != "ScenarioScoreCsvExporter")
                continue;

            var field = exporter.GetType().GetField("studentId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
                field.SetValue(exporter, studentId);
        }
    }
}
