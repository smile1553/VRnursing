using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

// Makes the left controller point and click too (UI buttons, quiz answers ...):
//   - the ray on Left_Controller is switched on while the left controller is on;
//   - when only the left controller is on, the right ray (Ray Interactor) is switched off,
//     so no ray hangs in the air from a controller that is not there.
// Created on its own at start; settings can be changed on the "HandRayFallback" object in Play.
public class HandRayFallback : MonoBehaviour
{
    [Tooltip("Show the left ray whenever the left controller is on. Off: only when the right controller is missing.")]
    public bool leftRayAlways = true;
    [Tooltip("Names of the objects that carry the left and right ray.")]
    public string leftRayObject = "Left_Controller";
    public string rightRayObject = "Ray Interactor";
    [Tooltip("A controller must be gone this long before its ray is switched off.")]
    public float lostSeconds = 1f;

    XRRayInteractor leftRay, rightRay;
    float leftSeen = -100f, rightSeen = -100f;
    float nextFind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindObjectOfType<HandRayFallback>() != null)
            return;
        new GameObject("HandRayFallback").AddComponent<HandRayFallback>();
    }

    void Update()
    {
        if ((leftRay == null || rightRay == null) && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 1f;
            leftRay = FindRay(leftRayObject);
            rightRay = FindRay(rightRayObject);
        }

        bool left = Present(XRNode.LeftHand, ref leftSeen);
        bool right = Present(XRNode.RightHand, ref rightSeen);
        if (!left && !right)
            return;   // no controllers (Editor / PC): leave everything as it is

        if (leftRay != null)
        {
            bool want = left && (leftRayAlways || !right);
            if (leftRay.enabled != want)
            {
                leftRay.enabled = want;
                Debug.Log($"[HandRayFallback] Left ray {(want ? "on" : "off")}");
            }
        }
        if (rightRay != null)
        {
            bool want = right || !left;
            if (rightRay.enabled != want)
            {
                rightRay.enabled = want;
                Debug.Log($"[HandRayFallback] Right ray {(want ? "on" : "off")}");
            }
        }
    }

    static XRRayInteractor FindRay(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponentInChildren<XRRayInteractor>(true) : null;
    }

    bool Present(XRNode node, ref float lastSeen)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        bool here = device.isValid;
        if (here && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
            here = tracked;
        if (here)
            lastSeen = Time.unscaledTime;
        return device.isValid && Time.unscaledTime - lastSeen <= Mathf.Max(0f, lostSeconds);
    }
}
