using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class XRCanvasAutoFix : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera customCamera;

    [Header("Settings")]
    [SerializeField] private bool fixEverythingInScene = true;

    private void Awake()
    {
        EnsureCanvasSetup();
    }

    private void OnEnable()
    {
        EnsureCanvasSetup();
    }

    [ContextMenu("Repair Canvas Setup")]
    public void EnsureCanvasSetup()
    {
        Camera targetCamera = customCamera != null ? customCamera : Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            if (canvas == null)
                continue;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null && targetCamera != null)
                canvas.worldCamera = targetCamera;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                canvas.worldCamera = null;
        }
    }
}
