using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class RightHandInput : MonoBehaviour
{
    [SerializeField] private InteractRaycaster raycaster;

    [Header("XR Controller")]
    [SerializeField] private bool useXRController = true;
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private float triggerThreshold = 0.75f;
    [SerializeField] private bool usePrimaryButton = true;
    [SerializeField] private bool useGripButton = false;

    [Header("Legacy Fallback (Editor)")]
    [SerializeField] private string clickButton = "Fire1";
    [SerializeField] private KeyCode clickKey = KeyCode.Mouse0;
    [SerializeField] private bool useButton = false;
    [SerializeField] private bool useKey = false;

    private InputDevice xrDevice;
    private bool triggerPressedLastFrame;
    private bool primaryPressedLastFrame;
    private bool gripPressedLastFrame;

    private void Awake()
    {
        if (raycaster == null)
        {
            raycaster = GetComponentInChildren<InteractRaycaster>();
        }
    }

    private void OnEnable()
    {
        xrDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        ResetXRState();
    }

    private void Update()
    {
        if (raycaster == null)
        {
            return;
        }

        bool clicked = useXRController && PollXRClickDown();

        if (!clicked && useButton && !string.IsNullOrEmpty(clickButton) && Input.GetButtonDown(clickButton))
        {
            clicked = true;
        }

        if (!clicked && useKey && Input.GetKeyDown(clickKey))
        {
            clicked = true;
        }

        if (clicked)
        {
            raycaster.EmitClick();
        }
    }

    private bool PollXRClickDown()
    {
        if (!xrDevice.isValid)
        {
            xrDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
            ResetXRState();
        }

        if (!xrDevice.isValid)
        {
            return false;
        }

        bool clicked = false;

        if (xrDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            bool triggerPressed = triggerValue >= triggerThreshold;
            if (triggerPressed && !triggerPressedLastFrame)
            {
                clicked = true;
            }
            triggerPressedLastFrame = triggerPressed;
        }
        else
        {
            triggerPressedLastFrame = false;
        }

        if (usePrimaryButton && xrDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed))
        {
            if (primaryPressed && !primaryPressedLastFrame)
            {
                clicked = true;
            }
            primaryPressedLastFrame = primaryPressed;
        }
        else
        {
            primaryPressedLastFrame = false;
        }

        if (useGripButton && xrDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed))
        {
            if (gripPressed && !gripPressedLastFrame)
            {
                clicked = true;
            }
            gripPressedLastFrame = gripPressed;
        }
        else
        {
            gripPressedLastFrame = false;
        }

        return clicked;
    }

    private void ResetXRState()
    {
        triggerPressedLastFrame = false;
        primaryPressedLastFrame = false;
        gripPressedLastFrame = false;
    }
}