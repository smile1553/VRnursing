using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class VRStudentIdKeypad : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TMP_InputField studentIdInput;
    [SerializeField] GameObject keypadRoot;

    [Header("Submit")]
    [SerializeField] GameObject submitTarget;
    [SerializeField] string submitMethodName = "Login";
    [SerializeField] UnityEvent onSubmit;

    [Header("Options")]
    [SerializeField] int maxLength = 12;
    [SerializeField] bool showOnInputSelected = true;
    [SerializeField] bool hideOnSubmit = true;

    void Awake()
    {
        if (studentIdInput == null)
            studentIdInput = GetComponentInChildren<TMP_InputField>(true);

        if (keypadRoot == null)
            keypadRoot = gameObject;
    }

    void OnEnable()
    {
        if (studentIdInput != null && showOnInputSelected)
            studentIdInput.onSelect.AddListener(OnInputSelected);
    }

    void OnDisable()
    {
        if (studentIdInput != null)
            studentIdInput.onSelect.RemoveListener(OnInputSelected);
    }

    public void Show()
    {
        if (keypadRoot != null)
            keypadRoot.SetActive(true);
    }

    public void Hide()
    {
        if (keypadRoot != null)
            keypadRoot.SetActive(false);
    }

    public void Append(string value)
    {
        if (studentIdInput == null || string.IsNullOrEmpty(value))
            return;

        if (maxLength > 0 && studentIdInput.text.Length >= maxLength)
            return;

        studentIdInput.text += value;
        studentIdInput.caretPosition = studentIdInput.text.Length;
    }

    public void Append0() => Append("0");
    public void Append1() => Append("1");
    public void Append2() => Append("2");
    public void Append3() => Append("3");
    public void Append4() => Append("4");
    public void Append5() => Append("5");
    public void Append6() => Append("6");
    public void Append7() => Append("7");
    public void Append8() => Append("8");
    public void Append9() => Append("9");

    public void Backspace()
    {
        if (studentIdInput == null || string.IsNullOrEmpty(studentIdInput.text))
            return;

        studentIdInput.text = studentIdInput.text.Substring(0, studentIdInput.text.Length - 1);
        studentIdInput.caretPosition = studentIdInput.text.Length;
    }

    public void Clear()
    {
        if (studentIdInput == null)
            return;

        studentIdInput.text = string.Empty;
        studentIdInput.caretPosition = 0;
    }

    public void Submit()
    {
        onSubmit?.Invoke();

        if (submitTarget != null && !string.IsNullOrEmpty(submitMethodName))
            submitTarget.SendMessage(submitMethodName, SendMessageOptions.DontRequireReceiver);

        if (hideOnSubmit)
            Hide();
    }

    void OnInputSelected(string _)
    {
        Show();
    }
}
