using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class VRKeyboardTrigger : MonoBehaviour
{
    [Header("Enter / Submit")]
    [SerializeField] GameObject nextTarget;
    [SerializeField] string nextMethodName = "Next";
    [SerializeField] UnityEvent onEnterPressed;
    [SerializeField] bool logSubmit;

    TMP_InputField inputField;

    void Start()
    {
        inputField = GetComponent<TMP_InputField>();

        if (inputField == null)
        {
            Debug.LogWarning("[VRKeyboardTrigger] TMP_InputField component not found.", this);
            return;
        }

        inputField.onSelect.AddListener(OnInputFieldSelected);
        inputField.onSubmit.AddListener(OnInputFieldSubmitted);
    }

    void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onSelect.RemoveListener(OnInputFieldSelected);
            inputField.onSubmit.RemoveListener(OnInputFieldSubmitted);
        }
    }

    void OnInputFieldSelected(string _)
    {
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
    }

    void OnInputFieldSubmitted(string _)
    {
        if (logSubmit)
            Debug.Log("[VRKeyboardTrigger] Input submitted, advancing next step.", this);

        onEnterPressed?.Invoke();

        if (nextTarget != null && !string.IsNullOrEmpty(nextMethodName))
        {
            nextTarget.SendMessage(nextMethodName, SendMessageOptions.DontRequireReceiver);
            return;
        }

        if (TryCallFirstBehaviourMethod("ScenarioController", "Next"))
            return;

        TryEmitUiNextSignal();
    }

    static bool TryCallFirstBehaviourMethod(string typeName, string methodName)
    {
        var behaviours = FindObjectsOfType<MonoBehaviour>();
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().Name != typeName)
                continue;

            var method = behaviour.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method == null)
                continue;

            method.Invoke(behaviour, null);
            return true;
        }

        return false;
    }

    static bool TryEmitUiNextSignal()
    {
        var signalBusType = FindTypeByName("UISignalBus");
        var emitMethod = signalBusType?.GetMethod("Emit", BindingFlags.Static | BindingFlags.Public);
        if (emitMethod == null)
            return false;

        emitMethod.Invoke(null, new object[] { "UI.Next", null });
        return true;
    }

    static Type FindTypeByName(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        return null;
    }
}
