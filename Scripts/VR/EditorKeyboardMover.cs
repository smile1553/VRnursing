using UnityEngine;

// Keyboard and mouse controls when there is no headset: in the Unity editor and in the PC
// (Windows) build. With a headset (Quest) it switches itself off and the controllers are used.
//
//   W / S  or  ↑ / ↓      forward / back
//   A / D                 step left / right
//   ← / →  or  J / L      turn left / right
//   Q / E                 down / up
//   hold right mouse button and move the mouse: look around (left/right and up/down)
//   hold Shift            faster
//
// It moves the whole player rig (the root of the main camera), the same thing the headset's
// joystick moves, so the story's "walk to the door" checks see it.
public class EditorKeyboardMover : MonoBehaviour
{
    [SerializeField] float walkSpeed = 1.4f;
    [SerializeField] float turnDegreesPerSecond = 90f;
    [SerializeField] float mouseDegreesPerPixel = 0.15f;
    [SerializeField] float fastMultiplier = 2.5f;
    [Tooltip("Speed of Q / E (down / up), metres per second.")]
    [SerializeField] float verticalSpeed = 1.0f;

    float pitch;

    // True while this is running, so VRJoystickMover leaves the keyboard alone (no double moves).
    public static bool Active { get; private set; }
    void OnEnable() { Active = true; }
    void OnDisable() { Active = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // Editor: always. Built app: only without a headset (the PC version).
        if (FindObjectOfType<EditorKeyboardMover>() != null)
            return;
        if (!Application.isEditor && UnityEngine.XR.XRSettings.isDeviceActive)
            return;
        GameObject go = new GameObject("EditorKeyboardMover (auto)");
        go.AddComponent<EditorKeyboardMover>();
        Debug.Log("[EditorKeyboardMover] Keyboard/mouse: W/S or Up/Down move, A/D step, Left/Right or J/L turn, Q/E down/up, right mouse drag to look, Shift = faster.");
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Transform head = cam.transform;
        Transform rig = head.root;
        if (rig == head)
            return;

        float forward = 0f, side = 0f, turn = 0f, vertical = 0f;
        bool fast = false, looking = false;
        Vector2 mouseDelta = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null)
        {
            // Do not walk while typing a student ID.
            if (TypingInTextField())
                return;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) forward += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) forward -= 1f;
            if (k.dKey.isPressed) side += 1f;
            if (k.aKey.isPressed) side -= 1f;
            if (k.rightArrowKey.isPressed || k.lKey.isPressed) turn += 1f;
            if (k.leftArrowKey.isPressed || k.jKey.isPressed) turn -= 1f;
            if (k.eKey.isPressed) vertical += 1f;
            if (k.qKey.isPressed) vertical -= 1f;
            fast = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
        }
        UnityEngine.InputSystem.Mouse m = UnityEngine.InputSystem.Mouse.current;
        if (m != null && m.rightButton.isPressed)
        {
            looking = true;
            mouseDelta = m.delta.ReadValue();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (TypingInTextField())
            return;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forward += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forward -= 1f;
        if (Input.GetKey(KeyCode.D)) side += 1f;
        if (Input.GetKey(KeyCode.A)) side -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.L)) turn += 1f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.J)) turn -= 1f;
        if (Input.GetKey(KeyCode.E)) vertical += 1f;
        if (Input.GetKey(KeyCode.Q)) vertical -= 1f;
        fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetMouseButton(1))
        {
            looking = true;
            mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
        }
#endif
        float dt = Time.deltaTime;
        float yaw = turn * turnDegreesPerSecond * dt;
        if (looking)
            yaw += mouseDelta.x * mouseDegreesPerPixel;
        if (Mathf.Abs(yaw) > 1e-4f)
            rig.RotateAround(head.position, Vector3.up, yaw);   // turn on the spot

        if (looking && !UnityEngine.XR.XRSettings.isDeviceActive)
        {
            // Up/down look, only without a headset (with one, the head itself does this).
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseDegreesPerPixel, -70f, 70f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        Vector3 move = flatForward * forward + flatRight * side;
        if (move.sqrMagnitude > 1f)
            move.Normalize();
        float speedScale = fast ? fastMultiplier : 1f;
        rig.position += move * walkSpeed * speedScale * dt;
        rig.position += Vector3.up * vertical * verticalSpeed * speedScale * dt;
    }

    static bool TypingInTextField()
    {
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected == null)
            return false;
        TMPro.TMP_InputField tmp = selected.GetComponent<TMPro.TMP_InputField>();
        if (tmp != null && tmp.isFocused)
            return true;
        UnityEngine.UI.InputField legacy = selected.GetComponent<UnityEngine.UI.InputField>();
        return legacy != null && legacy.isFocused;
    }
}
