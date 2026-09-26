using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class XRClickHaptics : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private float triggerThreshold = 0.75f;
    [SerializeField] private bool vibrateOnTrigger = true;
    [SerializeField] private bool vibrateOnPrimaryButton = true;
    [SerializeField] private bool vibrateOnGripButton = false;

    [Header("Haptics")]
    [SerializeField] private float amplitude = 0.45f;
    [SerializeField] private float duration = 0.08f;

    private InputDevice device;
    private bool triggerPressedLastFrame;
    private bool primaryPressedLastFrame;
    private bool gripPressedLastFrame;

    private void OnEnable()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
        ResetState();
    }

    private void Update()
    {
        if (!device.isValid)
        {
            device = InputDevices.GetDeviceAtXRNode(controllerNode);
            ResetState();
        }

        if (!device.isValid)
            return;

        bool clickDown = false;

        if (vibrateOnTrigger && device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            bool pressed = triggerValue >= triggerThreshold;
            if (pressed && !triggerPressedLastFrame)
                clickDown = true;

            triggerPressedLastFrame = pressed;
        }
        else
        {
            triggerPressedLastFrame = false;
        }

        if (vibrateOnPrimaryButton && device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed))
        {
            if (primaryPressed && !primaryPressedLastFrame)
                clickDown = true;

            primaryPressedLastFrame = primaryPressed;
        }
        else
        {
            primaryPressedLastFrame = false;
        }

        if (vibrateOnGripButton && device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed))
        {
            if (gripPressed && !gripPressedLastFrame)
                clickDown = true;

            gripPressedLastFrame = gripPressed;
        }
        else
        {
            gripPressedLastFrame = false;
        }

        if (clickDown)
            SendHaptic();
    }

    public void SendHaptic()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(controllerNode);

        if (!device.isValid)
            return;

        device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
    }

    private void ResetState()
    {
        triggerPressedLastFrame = false;
        primaryPressedLastFrame = false;
        gripPressedLastFrame = false;
    }
}
