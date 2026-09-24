#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps the Android INTERNET permission enabled even when this Assets-only
/// repository is imported into a Unity project with different PlayerSettings.
/// </summary>
public sealed class AndroidNetworkBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        if (!PlayerSettings.Android.forceInternetPermission)
        {
            PlayerSettings.Android.forceInternetPermission = true;
            Debug.Log("[AndroidBuild] Enabled INTERNET permission for server discovery and API connections.");
        }
    }
}
#endif
