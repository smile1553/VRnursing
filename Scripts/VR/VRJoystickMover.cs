using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class VRJoystickMover : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField] Transform rigRoot;
    [SerializeField] Transform head;

    [Header("Move")]
    [SerializeField] XRNode moveController = XRNode.LeftHand;
    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float deadzone = 0.2f;
    [SerializeField] bool useHeadDirection = true;

    [Header("Editor Test")]
    [SerializeField] bool enableKeyboardFallback = true;

    InputDevice moveDevice;

    void Awake()
    {
        if (rigRoot == null)
            rigRoot = transform;

        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    void OnEnable()
    {
        moveDevice = InputDevices.GetDeviceAtXRNode(moveController);
    }

    void Update()
    {
        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude < deadzone * deadzone)
            return;

        Vector3 forward;
        Vector3 right;

        if (useHeadDirection && head != null)
        {
            forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        }
        else
        {
            forward = Vector3.ProjectOnPlane(rigRoot.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(rigRoot.right, Vector3.up).normalized;
        }

        Vector3 move = (forward * input.y + right * input.x);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        rigRoot.position += move * moveSpeed * Time.deltaTime;
    }

    Vector2 ReadMoveInput()
    {
        if (!moveDevice.isValid)
            moveDevice = InputDevices.GetDeviceAtXRNode(moveController);

        if (moveDevice.isValid && moveDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return axis;

        if (!enableKeyboardFallback)
            return Vector2.zero;

        Vector2 keyboard = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyboard.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyboard.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboard.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboard.x -= 1f;
        return keyboard.normalized;
    }
}
