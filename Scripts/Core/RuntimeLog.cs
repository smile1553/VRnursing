using UnityEngine;

public static class RuntimeLog
{
    public static bool EnableInfo { get; set; }
    public static bool EnableWarnings { get; set; }

    public static void Info(object message)
    {
        if (EnableInfo)
            Debug.Log(message);
    }

    public static void Info(object message, Object context)
    {
        if (EnableInfo)
            Debug.Log(message, context);
    }

    public static void Warning(object message)
    {
        if (EnableWarnings)
            Debug.LogWarning(message);
    }

    public static void Warning(object message, Object context)
    {
        if (EnableWarnings)
            Debug.LogWarning(message, context);
    }
}
