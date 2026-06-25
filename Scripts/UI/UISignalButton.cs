using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public abstract class UISignalButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] bool logSignals;

    protected abstract string SignalName { get; }
    protected virtual object CreatePayload() => null;
    protected virtual string LogDetails => string.Empty;

    protected virtual void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    protected virtual void OnEnable() => button?.onClick.AddListener(EmitSignal);
    protected virtual void OnDisable() => button?.onClick.RemoveListener(EmitSignal);

    void EmitSignal()
    {
        if (string.IsNullOrEmpty(SignalName))
            return;

        if (logSignals)
            RuntimeLog.Info($"[{GetType().Name}] Emit {SignalName}{LogDetails}", this);

        UISignalBus.Emit(SignalName, CreatePayload());
    }
}
