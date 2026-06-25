using System;
using System.Collections.Generic;

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

        // Keep the legacy UI event available while forwarding it to the
        // application-wide bus used by SignalBridge, Story, and scoring.
        if (global::SignalBus.Instance != null)
            global::SignalBus.Instance.PublishUIEvent(signalName, ToCorePayload(payload));
    }

    static SignalPayload ToCorePayload(object payload)
    {
        if (payload is SignalPayload corePayload)
            return corePayload;

        var result = new SignalPayload();
        if (payload is IDictionary<string, object> values &&
            values.TryGetValue("choiceIndex", out object choice) && choice is int index)
        {
            result.choiceIndex = index;
        }
        return result;
    }
}
