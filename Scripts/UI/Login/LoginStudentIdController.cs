using System;
using System.Collections;
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

    [Header("Initial XR Pose")]
    [SerializeField] bool alignXrToLoginPanelOnStart = true;
    [SerializeField] Transform xrRig;
    [SerializeField] Vector3 initialCameraWorldPosition = new Vector3(5.173f, 1.077f, -1.985f);
    [SerializeField] float initialAlignDelayFrames = 3f;
    [SerializeField] int initialAlignAttempts = 6;

    [Header("Events")]
    [SerializeField] UnityEvent onLoginSucceeded;

    void Awake()
    {
        if (studentIdInput == null)
            studentIdInput = GetComponentInChildren<TMP_InputField>(true);

        if (loginScreenRoot == null)
            loginScreenRoot = gameObject;
    }

    void Start()
    {
        if (alignXrToLoginPanelOnStart)
            StartCoroutine(AlignXrToLoginPanel());
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

    IEnumerator AlignXrToLoginPanel()
    {
        int delayFrames = Mathf.Max(0, Mathf.RoundToInt(initialAlignDelayFrames));
        for (int i = 0; i < delayFrames; i++)
            yield return null;

        for (int i = 0; i < Mathf.Max(1, initialAlignAttempts); i++)
        {
            yield return null;
            ResolveXrRig();

            Camera cam = Camera.main;
            if (xrRig == null || cam == null || loginScreenRoot == null)
                continue;

            float yaw = GetYawFacing(loginScreenRoot.transform.position - initialCameraWorldPosition);
            MoveRigToCameraPose(cam.transform, initialCameraWorldPosition, yaw);
        }
    }

    void ResolveXrRig()
    {
        if (xrRig != null)
            return;

        GameObject found = GameObject.Find("XR_Origin_Pure");
        if (found == null)
            found = GameObject.Find("XR Origin");

        if (found != null)
            xrRig = found.transform;
    }

    void MoveRigToCameraPose(Transform cam, Vector3 cameraWorldPosition, float yawDegrees)
    {
        xrRig.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

        Vector3 cameraOffset = cam.position - xrRig.position;
        xrRig.position = cameraWorldPosition - cameraOffset;
    }

    static float GetYawFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return 0f;

        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }
}
