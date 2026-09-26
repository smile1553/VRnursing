using UnityEngine;
using UnityEngine.XR;

public enum PushToTalkButton
{
    Trigger,
    PrimaryButton,
    GripButton
}

[DisallowMultipleComponent]
public class RightHandInput : MonoBehaviour
{
    [SerializeField] private InteractRaycaster raycaster;

    [Header("Push-to-Talk")]
    [SerializeField] private AudioUploader audioUploader;
    [SerializeField] private bool enablePushToTalk = true;
    [SerializeField] private PushToTalkButton pushToTalkButton = PushToTalkButton.GripButton;
    [SerializeField] private bool usePushToTalkKeyInEditor = true;
    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Space;

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
    private bool pushToTalkPressedLastFrame;

    private void Awake()
    {
        if (raycaster == null)
        {
            raycaster = GetComponentInChildren<InteractRaycaster>();
        }
        if (audioUploader == null)
        {
            audioUploader = FindObjectOfType<AudioUploader>();
        }
    }

    private void OnEnable()
    {
        xrDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
        ResetXRState();
    }

    private void Update()
    {
        PollPushToTalk();

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

    private void OnDisable()
    {
        if (pushToTalkPressedLastFrame && audioUploader != null)
            audioUploader.StopPushToTalkAndUpload();
        ResetXRState();
    }

    private void PollPushToTalk()
    {
        if (!enablePushToTalk || audioUploader == null)
            return;

        bool pressed = false;
        bool hasXRValue = false;
        if (useXRController)
        {
            if (!xrDevice.isValid)
                xrDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
            hasXRValue = TryReadPushToTalkButton(out pressed);
        }

        if (usePushToTalkKeyInEditor)
        {
            if (Input.GetKeyDown(pushToTalkKey))
                audioUploader.StartPushToTalk();
            if (Input.GetKeyUp(pushToTalkKey))
                audioUploader.StopPushToTalkAndUpload();
        }

        if (!hasXRValue)
        {
            if (pushToTalkPressedLastFrame)
                audioUploader.StopPushToTalkAndUpload();
            pushToTalkPressedLastFrame = false;
            return;
        }

        if (pressed && !pushToTalkPressedLastFrame)
            audioUploader.StartPushToTalk();
        else if (!pressed && pushToTalkPressedLastFrame)
            audioUploader.StopPushToTalkAndUpload();

        pushToTalkPressedLastFrame = pressed;
    }

    private bool TryReadPushToTalkButton(out bool pressed)
    {
        pressed = false;
        if (!xrDevice.isValid)
            return false;

        switch (pushToTalkButton)
        {
            case PushToTalkButton.Trigger:
                float triggerValue;
                if (!xrDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
                    return false;
                pressed = triggerValue >= triggerThreshold;
                return true;

            case PushToTalkButton.PrimaryButton:
                return xrDevice.TryGetFeatureValue(CommonUsages.primaryButton, out pressed);

            default:
                return xrDevice.TryGetFeatureValue(CommonUsages.gripButton, out pressed);
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
        pushToTalkPressedLastFrame = false;
    }
}
