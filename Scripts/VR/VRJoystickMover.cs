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

    [Header("Vertical Move")]
    [SerializeField] bool enableVerticalMove = true;
    [SerializeField] XRNode verticalMoveController = XRNode.RightHand;
    [SerializeField] float verticalMoveSpeed = 1.0f;
    [SerializeField] bool invertVerticalMove = false;

    [Header("Turn")]
    [SerializeField] bool enableKeyboardTurn = true;
    [SerializeField] float keyboardTurnSpeed = 80f;
    [SerializeField] bool enableSnapTurn = true;
    [SerializeField] XRNode snapTurnController = XRNode.RightHand;
    [SerializeField] float snapTurnAngle = 30f;
    [SerializeField] float snapTurnThreshold = 0.75f;
    [SerializeField] float snapTurnCooldown = 0.35f;

    [Header("Editor Test")]
    [SerializeField] bool enableKeyboardFallback = true;

    InputDevice moveDevice;
    InputDevice verticalMoveDevice;
    InputDevice snapTurnDevice;
    float nextSnapTurnTime;

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
        verticalMoveDevice = InputDevices.GetDeviceAtXRNode(verticalMoveController);
        snapTurnDevice = InputDevices.GetDeviceAtXRNode(snapTurnController);
    }

    void Update()
    {
        HandleTurning();

        Vector2 input = ReadMoveInput();
        float verticalInput = ReadVerticalInput();

        if (input.sqrMagnitude < deadzone * deadzone && Mathf.Abs(verticalInput) < deadzone)
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

        Vector3 move = Vector3.zero;

        if (input.sqrMagnitude >= deadzone * deadzone)
            move += forward * input.y + right * input.x;

        if (enableVerticalMove && Mathf.Abs(verticalInput) >= deadzone)
            move += Vector3.up * verticalInput * verticalMoveSpeed / Mathf.Max(0.01f, moveSpeed);

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        rigRoot.position += move * moveSpeed * Time.deltaTime;
    }

    void HandleTurning()
    {
        float continuousTurn = ReadKeyboardTurnInput();
        if (Mathf.Abs(continuousTurn) > 0.01f)
            rigRoot.Rotate(Vector3.up, continuousTurn * keyboardTurnSpeed * Time.deltaTime, Space.World);

        float snapInput = ReadSnapTurnInput();
        if (enableSnapTurn && Mathf.Abs(snapInput) >= snapTurnThreshold && Time.time >= nextSnapTurnTime)
        {
            float direction = Mathf.Sign(snapInput);
            rigRoot.Rotate(Vector3.up, direction * snapTurnAngle, Space.World);
            nextSnapTurnTime = Time.time + Mathf.Max(0.05f, snapTurnCooldown);
        }
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

    float ReadVerticalInput()
    {
        if (!enableVerticalMove)
            return 0f;

        if (!verticalMoveDevice.isValid)
            verticalMoveDevice = InputDevices.GetDeviceAtXRNode(verticalMoveController);

        if (verticalMoveDevice.isValid && verticalMoveDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
        {
            float value = axis.y;
            return invertVerticalMove ? -value : value;
        }

        if (!enableKeyboardFallback)
            return 0f;

        float keyboard = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.PageUp)) keyboard += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.PageDown)) keyboard -= 1f;
        return invertVerticalMove ? -keyboard : keyboard;
    }

    float ReadKeyboardTurnInput()
    {
        if (!enableKeyboardFallback || !enableKeyboardTurn)
            return 0f;

        float turn = 0f;
        if (Input.GetKey(KeyCode.J)) turn -= 1f;
        if (Input.GetKey(KeyCode.L)) turn += 1f;
        return turn;
    }

    float ReadSnapTurnInput()
    {
        if (!enableSnapTurn)
            return 0f;

        if (!snapTurnDevice.isValid)
            snapTurnDevice = InputDevices.GetDeviceAtXRNode(snapTurnController);

        if (snapTurnDevice.isValid && snapTurnDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return axis.x;

        return 0f;
    }
}
