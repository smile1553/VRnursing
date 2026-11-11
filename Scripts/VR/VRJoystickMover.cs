using UnityEngine;
using UnityEngine.XR;

// Controller layout (both controllers):
//   left stick   forward/back = walk (where you look), left/right = step sideways
//   right stick  left/right = turn, forward/back = float up/down
//                (only the bigger direction counts, so a diagonal push does one thing)
//   X or A       height back to where it started
// With only one controller (the other one broken or off), kept simple:
//   stick        forward/back = walk, left/right = turn (no floating); X/A = height back
// Keyboard (Editor / PC): WASD or arrows move, Q/E down/up, J/L turn.
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
    [Tooltip("On: keep pushing the stick to keep turning. Off: turn one step (Snap Turn Angle) per push.")]
    [SerializeField] bool smoothTurn = true;
    [Tooltip("Degrees per second when Smooth Turn is on.")]
    [SerializeField] float smoothTurnSpeed = 60f;
    [SerializeField] float snapTurnAngle = 30f;
    [SerializeField] float snapTurnThreshold = 0.75f;
    [SerializeField] float snapTurnCooldown = 0.35f;

    [Header("Height Reset (X / A)")]
    [SerializeField] bool enableHeightReset = true;

    [Header("One Controller")]
    [Tooltip("When only one controller is on, its stick walks (forward/back) and turns (left/right).")]
    [SerializeField] bool supportOneController = true;
    [Tooltip("A controller must be gone this long before the layout switches (so a short tracking loss does not flip it).")]
    [SerializeField] float controllerLostSeconds = 1f;

    [Header("Editor Test")]
    [SerializeField] bool enableKeyboardFallback = true;

    InputDevice moveDevice;
    InputDevice turnDevice;
    float nextSnapTurnTime;
    float startHeight;
    bool heightButtonWasDown;
    float moveDeviceLastSeen = -100f;
    float turnDeviceLastSeen = -100f;
    bool snapReleased = true;

    void Awake()
    {
        if (rigRoot == null)
            rigRoot = transform;

        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        startHeight = rigRoot.position.y;
    }

    void OnEnable()
    {
        moveDevice = InputDevices.GetDeviceAtXRNode(moveController);
        turnDevice = InputDevices.GetDeviceAtXRNode(snapTurnController);
    }

    void Update()
    {
        bool hasMove = IsPresent(ref moveDevice, moveController, ref moveDeviceLastSeen);
        bool hasTurn = IsPresent(ref turnDevice, snapTurnController, ref turnDeviceLastSeen);

        Vector2 move = Vector2.zero;   // x = sideways, y = forward
        float vertical = 0f;
        float turn = 0f;

        if (hasMove && hasTurn)
        {
            move = DeadZone(ReadAxis(moveDevice));
            Vector2 r = DeadZone(ReadAxis(turnDevice));
            if (Mathf.Abs(r.x) >= Mathf.Abs(r.y))
                turn = r.x;
            else
                vertical = r.y;
        }
        else if (supportOneController && (hasMove || hasTurn))
        {
            // Kept simple on purpose: walk and turn only (no floating, no stepping sideways).
            InputDevice only = hasMove ? moveDevice : turnDevice;
            Vector2 a = DeadZone(ReadAxis(only));
            if (Mathf.Abs(a.x) >= Mathf.Abs(a.y))
                turn = a.x;
            else
                move.y = a.y;
        }
        else if (hasMove)
        {
            move = DeadZone(ReadAxis(moveDevice));
        }

        // The keyboard/mouse script (EditorKeyboardMover) handles the keyboard when it runs.
        if (enableKeyboardFallback && !EditorKeyboardMover.Active)
        {
            if (move == Vector2.zero)
                move = ReadKeyboardMove();
            if (vertical == 0f)
                vertical = ReadKeyboardVertical();
        }

        if (invertVerticalMove)
            vertical = -vertical;
        if (!enableVerticalMove)
            vertical = 0f;

        HandleTurning(turn);
        HandleHeightReset(hasMove, hasTurn);
        ApplyMove(move, vertical);
    }

    bool IsPresent(ref InputDevice device, XRNode node, ref float lastSeen)
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(node);

        bool here = device.isValid;
        if (here && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
            here = tracked;
        if (here)
            lastSeen = Time.unscaledTime;
        return device.isValid && Time.unscaledTime - lastSeen <= Mathf.Max(0f, controllerLostSeconds);
    }

    static Vector2 ReadAxis(InputDevice device)
    {
        if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return axis;
        return Vector2.zero;
    }

    Vector2 DeadZone(Vector2 v)
    {
        return v.sqrMagnitude < deadzone * deadzone ? Vector2.zero : v;
    }

    void ApplyMove(Vector2 input, float vertical)
    {
        if (input == Vector2.zero && Mathf.Abs(vertical) < 0.01f)
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

        Vector3 move = forward * input.y + right * input.x;
        move += Vector3.up * vertical * verticalMoveSpeed / Mathf.Max(0.01f, moveSpeed);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        rigRoot.position += move * moveSpeed * Time.deltaTime;
    }

    void HandleTurning(float stick)
    {
        float keyboard = ReadKeyboardTurn();
        if (Mathf.Abs(keyboard) > 0.01f)
            TurnBy(keyboard * keyboardTurnSpeed * Time.deltaTime);

        if (!enableSnapTurn)
            return;

        if (smoothTurn)
        {
            if (Mathf.Abs(stick) > 0.01f)
                TurnBy(stick * smoothTurnSpeed * Time.deltaTime);
            return;
        }

        if (Mathf.Abs(stick) < snapTurnThreshold * 0.5f)
            snapReleased = true;
        if (Mathf.Abs(stick) >= snapTurnThreshold && snapReleased && Time.time >= nextSnapTurnTime)
        {
            TurnBy(Mathf.Sign(stick) * snapTurnAngle);
            nextSnapTurnTime = Time.time + Mathf.Max(0.05f, snapTurnCooldown);
            snapReleased = false;
        }
    }

    // Turns around the head, so the view does not slide sideways while turning.
    void TurnBy(float degrees)
    {
        if (head != null)
            rigRoot.RotateAround(head.position, Vector3.up, degrees);
        else
            rigRoot.Rotate(Vector3.up, degrees, Space.World);
    }

    // X (left) or A (right): back to the starting height.
    void HandleHeightReset(bool hasMove, bool hasTurn)
    {
        if (!enableHeightReset)
            return;
        bool down = false;
        if (hasMove && moveDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool x) && x)
            down = true;
        if (hasTurn && turnDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool a) && a)
            down = true;
        if (down && !heightButtonWasDown)
        {
            Vector3 p = rigRoot.position;
            rigRoot.position = new Vector3(p.x, startHeight, p.z);
        }
        heightButtonWasDown = down;
    }

    static Vector2 ReadKeyboardMove()
    {
        Vector2 keyboard = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyboard.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyboard.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboard.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboard.x -= 1f;
        return keyboard.normalized;
    }

    static float ReadKeyboardVertical()
    {
        float keyboard = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.PageUp)) keyboard += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.PageDown)) keyboard -= 1f;
        return keyboard;
    }

    float ReadKeyboardTurn()
    {
        if (!enableKeyboardFallback || !enableKeyboardTurn || EditorKeyboardMover.Active)
            return 0f;

        float turn = 0f;
        if (Input.GetKey(KeyCode.J)) turn -= 1f;
        if (Input.GetKey(KeyCode.L)) turn += 1f;
        return turn;
    }
}
