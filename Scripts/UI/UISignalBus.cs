using System;

public static class UISignalBus
{
    public static event Action<string, object> OnSignal;

    public static void Emit(string signalName, object payload)
    {
        if (string.IsNullOrEmpty(signalName))
        {
            return;
        }

        OnSignal?.Invoke(signalName, payload);
    }
}
