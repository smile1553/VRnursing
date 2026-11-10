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
    [Tooltip("Off: the A button does not click any more (it resets the height, see VRJoystickMover).")]
    [SerializeField] private bool usePrimaryButton = false;
    [SerializeField] private bool useGripButton = false;

    [Header("Other Hand")]
    [Tooltip("The left trigger clicks too, with the ray from the left hand.")]
    [SerializeField] private bool alsoOtherHand = true;
    [SerializeField] private XRNode otherControllerNode = XRNode.LeftHand;
    [Tooltip("Where the left ray starts. Empty = the object named Left_Controller.")]
    [SerializeField] private Transform otherRayOrigin;

    [Header("Legacy Fallback (Editor)")]
    [SerializeField] private string clickButton = "Fire1";
    [SerializeField] private KeyCode clickKey = KeyCode.Mouse0;
    [SerializeField] private bool useButton = false;
    [SerializeField] private bool useKey = false;

    private InputDevice xrDevice;
    private InputDevice otherDevice;
    private bool otherTriggerLastFrame;
    private Transform mainRayOrigin;
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

        if (mainRayOrigin == null)
            mainRayOrigin = raycaster.RayOrigin;

        bool clicked = useXRController && PollXRClickDown();
        if (!clicked && useXRController && alsoOtherHand && PollOtherHandClickDown())
        {
            // Click from the left hand: cast the ray from the left controller this once.
            Transform left = ResolveOtherRayOrigin();
            if (left != null)
            {
                raycaster.SetRayOrigin(left);
                raycaster.EmitClick();
                raycaster.SetRayOrigin(MainHandPresent() ? mainRayOrigin : left);
                return;
            }
        }
        else if (alsoOtherHand && !MainHandPresent() && OtherHandPresent())
        {
            // Only the left controller is on: its ray is the one that points.
            Transform left = ResolveOtherRayOrigin();
            if (left != null)
                raycaster.SetRayOrigin(left);
        }
        else if (mainRayOrigin != null && raycaster.RayOrigin != mainRayOrigin)
        {
            raycaster.SetRayOrigin(mainRayOrigin);
        }

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

    private void OnDisable()
    {
        ResetXRState();
    }

    private bool MainHandPresent()
    {
        if (!xrDevice.isValid)
            xrDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!xrDevice.isValid)
            return false;
        return !xrDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked;
    }

    private bool OtherHandPresent()
    {
        if (!otherDevice.isValid)
            otherDevice = InputDevices.GetDeviceAtXRNode(otherControllerNode);
        if (!otherDevice.isValid)
            return false;
        return !otherDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked;
    }

    private Transform ResolveOtherRayOrigin()
    {
        if (otherRayOrigin == null)
        {
            GameObject found = GameObject.Find("Left_Controller");
            if (found != null)
                otherRayOrigin = found.transform;
        }
        return otherRayOrigin;
    }

    private bool PollOtherHandClickDown()
    {
        if (!otherDevice.isValid)
            otherDevice = InputDevices.GetDeviceAtXRNode(otherControllerNode);
        if (!otherDevice.isValid)
        {
            otherTriggerLastFrame = false;
            return false;
        }
        bool pressed = otherDevice.TryGetFeatureValue(CommonUsages.trigger, out float value) && value >= triggerThreshold;
        bool clicked = pressed && !otherTriggerLastFrame;
        otherTriggerLastFrame = pressed;
        return clicked;
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
