using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using TMPro;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

public class Part3VisualDemoController : MonoBehaviour
{
    [Header("Sticker")]
    [SerializeField] private GameObject stickerObject;
    [SerializeField] private Sprite stickerSprite;
    [SerializeField] private Transform stickerAnchor;
    [SerializeField] private Vector3 fallbackStickerCameraOffset = new Vector3(-0.30f, -0.17f, 0.9f);
    [Tooltip("Sticker sits on the LEFT, in the same column as the medical-record button, just below it (moved further down/up automatically if a prompt or dialogue box is in the way).")]
    [SerializeField] private bool alignStickerWithRecordButton = true;
    [Tooltip("Extra up (+) / down (-) shift in metres after the automatic placement.")]
    [SerializeField] private float stickerRaise = 0.028f;
    [Tooltip("Size of the sticker in the corner of the view (1 = original size).")]
    [SerializeField] private float hudStickerSizeMultiplier = 0.8f;
    [SerializeField] private bool forceStickerSymmetricHudOffset = true;
    [SerializeField] private bool keepStickerInCameraCorner = true;
    // Keep the bear sticker close to the size of the medical-record card in world space.
    [SerializeField] private Vector2 fallbackStickerSize = new Vector2(0.038f, 0.038f);
    [SerializeField] private float fallbackStickerWorldHeight = 0.14f;
    [SerializeField] private Transform yayaStickerAttachTarget;
    [SerializeField] private string yayaStickerAttachTargetNames = "Bear_Sticker_Target|BearSticker_Target|bow_bear (1)|bow_bear|bear.002|bears";
    [SerializeField] private float stickerAttachDistance = 0.22f;
    [SerializeField] private GameObject stickerAttachAnimationPrefab;
    [SerializeField] private string stickerAttachAnimationPrefabNames = "Bear_Sticker|BearSticker";
    [SerializeField] private string stickerAttachAnimationState = "";
    [SerializeField] private float stickerAttachAnimationDuration = 1.5f;
    [SerializeField] private float placedStickerWorldHeight = 0.09f;
    [SerializeField] private Vector2 hudStickerColliderWorldSize = new Vector2(0.18f, 0.18f);
    [SerializeField] private Vector2 placedStickerColliderWorldSize = new Vector2(0.12f, 0.12f);
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popClip;
    [SerializeField] private float stickerPopDuration = 0.3f;
    [SerializeField] private float stickerHighlightDuration = 0.35f;
    [SerializeField] private UnityEvent onHudStickerSelected;

    [Header("Stethoscope")]
    [SerializeField] private Transform stethoscope;
    [SerializeField] private Transform nurseHandAnchor;
    [SerializeField] private Transform yayaHandAnchor;
    [SerializeField] private Transform momHandAnchor;
    [SerializeField] private Transform momChestAnchor;
    [SerializeField] private Transform yayaChestAnchor;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private float stethoscopeMoveDuration = 0.65f;
    [SerializeField] private float stethoscopePulseDuration = 0.45f;
    [SerializeField] private float heartbeatDuration = 3.2f;
    [Tooltip("How many times louder than the clip itself (1 = as recorded).")]
    [Range(1f, 5f)] [SerializeField] private float heartbeatLoudness = 3f;

    private Coroutine stickerRoutine;
    private Coroutine hudStickerAttachRoutine;
    private GameObject stickerPrefabSource;
    private Coroutine stethoscopeRoutine;
    private Vector3 stickerBaseScale = Vector3.one;
    private Vector3 stethoscopeBaseScale = Vector3.one;
    private bool stickerHudVisible;
    [Tooltip("How much Yaya's upset value (0-100) drops when she is given a sticker.")]
    [SerializeField] private float stickerCalmAmount = 12f;
    [Tooltip("Sticker on the bear: how far below its middle (part of its half height) and how far out from its middle (part of its half depth; bigger = further from the bear).")]
    [SerializeField] private float stickerOnBearDrop = 0.12f;
    [SerializeField] private float stickerOnBearOut = 0.5f;
    public Transform HudSticker => stickerObject != null ? stickerObject.transform : null;
    private bool stickerTemporarilyHiddenForQuiz;
    private bool hudStickerWasSelected;
    private bool hudStickerAttaching;
    private XRSimpleInteractable hudStickerInteractable;

    public bool HudStickerWasSelected => hudStickerWasSelected;
    public bool IsHudStickerAttaching => hudStickerAttaching;
    public GameObject StickerObject => stickerObject;

    public void ResetHudStickerSelection()
    {
        hudStickerWasSelected = false;
    }

    private void Awake()
    {
        ResolveReferences();

        if (stickerObject != null)
        {
            stickerBaseScale = stickerObject.transform.localScale == Vector3.zero
                ? Vector3.one
                : stickerObject.transform.localScale;
            stickerObject.SetActive(false);
        }

        if (stethoscope != null)
            stethoscopeBaseScale = stethoscope.localScale == Vector3.zero ? Vector3.one : stethoscope.localScale;
    }

    public void ShowSticker()
    {
        stickerTemporarilyHiddenForQuiz = false;
        if (stickerObject != null && !stickerObject.activeSelf)
            hudStickerWasSelected = false;
        if (rewardStyle)
            return;   // shown by BeginStickerGive, only when a step asks for it

        if (stickerRoutine != null)
            StopCoroutine(stickerRoutine);

        stickerRoutine = StartCoroutine(ShowStickerRoutine());
    }

    public void HighlightSticker()
    {
        if (rewardStyle)
            return;
        if (stickerRoutine != null)
            StopCoroutine(stickerRoutine);

        stickerRoutine = StartCoroutine(StickerPulseRoutine());
    }

    public void HideSticker()
    {
        StopRewardReveal();
        stickerHudVisible = false;
        if (stickerRoutine != null)
        {
            StopCoroutine(stickerRoutine);
            stickerRoutine = null;
        }
        if (stickerObject != null)
            stickerObject.SetActive(false);
    }


    public void TemporarilyHideStickerForQuiz()
    {
        StopRewardReveal();
        stickerTemporarilyHiddenForQuiz = true;
        if (stickerRoutine != null)
        {
            StopCoroutine(stickerRoutine);
            stickerRoutine = null;
        }
        if (stickerObject != null)
            stickerObject.SetActive(false);
    }

    public void RestoreStickerAfterQuiz()
    {
        stickerTemporarilyHiddenForQuiz = false;
        if (rewardStyle)
            return;   // not kept in the corner any more
        ResolveReferences();
        stickerHudVisible = true;
        if (keepStickerInCameraCorner)
            PlaceFallbackStickerNearCamera();
        else if (stickerAnchor != null && stickerObject != null)
            stickerObject.transform.SetPositionAndRotation(stickerAnchor.position, stickerAnchor.rotation);
        if (stickerObject != null)
        {
            stickerObject.SetActive(true);
            EnsureHudStickerInteractable();
        }
    }
    private void LateUpdate()
    {
        if (rewardStyle)
            UpdateRewardSticker();
        else if (!stickerTemporarilyHiddenForQuiz && !hudStickerAttaching && stickerHudVisible && keepStickerInCameraCorner)
            PlaceFallbackStickerNearCamera();

        UpdateStickerGlow();
        TrimRecordButtonHitArea();
        UpdateVrStickerClick();
        UpdateMouseStickerClick();
    }

    // Mouse in the Game view (editor / no headset): clicking the sticker gives it. OnMouseDown
    // on the sticker does not fire when the project uses the new Input System only.
    private int lastMouseStickerFrame = -1;

    private void UpdateMouseStickerClick()
    {
        bool clicked = false;
        Vector2 point = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            clicked = true;
            point = mouse.position.ReadValue();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            point = Input.mousePosition;
        }
#endif
        if (!clicked || Time.frameCount == lastMouseStickerFrame)
            return;
        if (!stickerGiveEnabled || !stickerHudVisible || hudStickerAttaching || stickerObject == null || !stickerObject.activeInHierarchy)
            return;
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(point);
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != null && hit.collider.transform.IsChildOf(stickerObject.transform))
            {
                lastMouseStickerFrame = Time.frameCount;
                HandleHudStickerClicked();
                return;
            }
        }
        // No collider hit: fall back to the sticker's picture on screen.
        Renderer picture = stickerObject.GetComponentInChildren<Renderer>();
        if (picture != null && picture.bounds.IntersectRay(ray))
        {
            lastMouseStickerFrame = Time.frameCount;
            HandleHudStickerClicked();
        }
    }

    // The medical-record button's clickable area is a square that is bigger than its icon and
    // reaches down over the bear sticker. Trim it to the icon, and never lower than the top of
    // the sticker, so clicking the sticker gives the sticker.
    private UnityEngine.UI.Image recordButtonImage;
    private float nextRecordButtonScan;

    private void TrimRecordButtonHitArea()
    {
        if (recordButtonImage == null)
        {
            if (Time.unscaledTime < nextRecordButtonScan)
                return;
            nextRecordButtonScan = Time.unscaledTime + 1f;
            MedicalRecordHudButton hud = FindObjectOfType<MedicalRecordHudButton>(true);
            UnityEngine.UI.Selectable selectable = hud != null ? hud.GetComponentInChildren<UnityEngine.UI.Selectable>(true) : null;
            if (selectable != null)
                recordButtonImage = selectable.targetGraphic as UnityEngine.UI.Image ?? selectable.GetComponent<UnityEngine.UI.Image>();
            if (recordButtonImage == null)
                return;
        }

        RectTransform rectTransform = recordButtonImage.rectTransform;
        Rect rect = rectTransform.rect;
        float bottom = 0f;
        if (stickerObject != null && stickerObject.activeInHierarchy && stickerHudVisible && !hudStickerAttaching)
        {
            float halfHeight = Mathf.Max(0.01f, fallbackStickerWorldHeight * hudStickerSizeMultiplier) * 0.5f;
            Vector3 stickerTop = stickerObject.transform.position + stickerObject.transform.up * halfHeight;
            float localTop = rectTransform.InverseTransformPoint(stickerTop).y;
            bottom = Mathf.Clamp(localTop - rect.yMin - rect.height * 0.05f, bottom, rect.height * 0.7f);
        }
        float side = rect.width * 0.04f;
        float top = 0f;
        recordButtonImage.raycastPadding = new Vector4(side, bottom, side, top);

        // Controller rays ignore the padding above, so the icon itself stops catching clicks
        // and an invisible, smaller child does it instead (clicks on it still reach the button).
        if (recordButtonHitArea == null)
        {
            GameObject hit = new GameObject("HitArea", typeof(RectTransform));
            hit.layer = recordButtonImage.gameObject.layer;
            recordButtonHitArea = (RectTransform)hit.transform;
            recordButtonHitArea.SetParent(rectTransform, false);
            recordButtonHitArea.anchorMin = Vector2.zero;
            recordButtonHitArea.anchorMax = Vector2.one;
            UnityEngine.UI.Image area = hit.AddComponent<UnityEngine.UI.Image>();
            area.color = new Color(1f, 1f, 1f, 0f);
            area.raycastTarget = true;
            hit.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            foreach (UnityEngine.UI.Graphic graphic in rectTransform.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (graphic != area)
                    graphic.raycastTarget = false;
            }
        }
        recordButtonHitArea.offsetMin = new Vector2(side, bottom);
        recordButtonHitArea.offsetMax = new Vector2(-side, -top);
    }
    private RectTransform recordButtonHitArea;

    public void HighlightStethoscope()
    {
        if (stethoscopeRoutine != null)
            StopCoroutine(stethoscopeRoutine);

        stethoscopeRoutine = StartCoroutine(PulseTransformRoutine(stethoscope, stethoscopeBaseScale, stethoscopePulseDuration));
    }

    public IEnumerator MoveStethoscopeForYayaListeningMom()
    {
        yield return MoveStethoscopeSequence(nurseHandAnchor, yayaHandAnchor, momChestAnchor);
    }

    public IEnumerator MoveStethoscopeForMomListeningYaya()
    {
        yield return MoveStethoscopeSequence(momHandAnchor, yayaChestAnchor);
    }

    public IEnumerator PlayHeartbeat()
    {
        ResolveReferences();

        if (audioSource == null || heartbeatClip == null)
        {
            yield return new WaitForSeconds(heartbeatDuration);
            yield break;
        }

        audioSource.Stop();
        audioSource.clip = heartbeatClip;
        audioSource.loop = true;
        audioSource.volume = 1f;
        audioSource.Play();
        AudioBoost.Play(audioSource, heartbeatLoudness);
        yield return new WaitForSeconds(Mathf.Max(0.1f, heartbeatDuration));
        audioSource.Stop();
        AudioBoost.Stop(audioSource);
        audioSource.loop = false;
    }

    private IEnumerator ShowStickerRoutine()
    {
        ResolveReferences();

        if (stickerObject == null)
            yield break;

        if (keepStickerInCameraCorner)
            PlaceFallbackStickerNearCamera();
        else if (stickerAnchor != null)
            stickerObject.transform.SetPositionAndRotation(stickerAnchor.position, stickerAnchor.rotation);
        else
            PlaceFallbackStickerNearCamera();

        stickerHudVisible = true;
        stickerObject.SetActive(true);
        EnsureHudStickerInteractable();
        stickerObject.transform.localScale = Vector3.zero;
        PlayOneShot(popClip);

        yield return ScaleRoutine(stickerObject.transform, Vector3.zero, stickerBaseScale * 1.2f, stickerPopDuration * 0.65f);
        yield return ScaleRoutine(stickerObject.transform, stickerBaseScale * 1.2f, stickerBaseScale, stickerPopDuration * 0.35f);
        stickerRoutine = null;
    }

    private IEnumerator StickerPulseRoutine()
    {
        ResolveReferences();

        if (stickerObject == null)
            yield break;

        if (!stickerObject.activeSelf)
        {
            PlaceFallbackStickerNearCamera();
            stickerObject.SetActive(true);
        }
        stickerHudVisible = true;
        EnsureHudStickerInteractable();
        if (keepStickerInCameraCorner)
            PlaceFallbackStickerNearCamera();

        PlayOneShot(popClip);
        yield return ScaleRoutine(stickerObject.transform, stickerBaseScale, stickerBaseScale * 1.08f, stickerHighlightDuration * 0.5f);
        yield return ScaleRoutine(stickerObject.transform, stickerBaseScale * 1.08f, stickerBaseScale, stickerHighlightDuration * 0.5f);
        stickerRoutine = null;
    }

    private IEnumerator MoveStethoscopeSequence(params Transform[] anchors)
    {
        ResolveReferences();

        if (stethoscope == null || anchors == null || anchors.Length == 0)
            yield break;

        foreach (Transform anchor in anchors)
        {
            if (anchor == null)
                continue;

            yield return MoveToAnchor(stethoscope, anchor, stethoscopeMoveDuration);
        }
    }

    private IEnumerator MoveToAnchor(Transform target, Transform anchor, float duration)
    {
        Vector3 startPosition = target.position;
        Quaternion startRotation = target.rotation;
        Vector3 endPosition = anchor.position;
        Quaternion endRotation = anchor.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Smooth(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            target.position = Vector3.Lerp(startPosition, endPosition, t);
            target.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        target.SetPositionAndRotation(endPosition, endRotation);
    }

    private IEnumerator PulseTransformRoutine(Transform target, Vector3 baseScale, float duration)
    {
        if (target == null)
            yield break;

        yield return ScaleRoutine(target, baseScale, baseScale * 1.08f, duration * 0.5f);
        yield return ScaleRoutine(target, baseScale * 1.08f, baseScale, duration * 0.5f);
    }

    private IEnumerator ScaleRoutine(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (target == null)
            yield break;

        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        target.localScale = to;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }


    private void NormalizeStickerHudOffset()
    {
        if (!forceStickerSymmetricHudOffset)
            return;

        fallbackStickerCameraOffset = new Vector3(-0.30f, -0.17f, 0.9f);
    }

    private bool stickerColumnSolved;
    private float stickerColumnSolveTime;
    private Vector3 stickerColumnOffset;

    // ------------------------------------------------------------------------------
    // Giving the sticker: only possible while a prompt asks for it. During that time the
    // sticker glows and pulses, and it can be clicked with the mouse or a VR controller ray.
    // ------------------------------------------------------------------------------
    [Header("Sticker giving")]
    [SerializeField] private Color stickerGlowColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private float stickerGlowPulseSpeed = 0.7f;
    private bool stickerGiveEnabled;
    private GameObject placedStickerCopy;
    private SpriteRenderer stickerGlow;
    private XRRayInteractor[] rayInteractors;
    private float nextRayScanTime;
    private bool vrTriggerWasDown;
    private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

    public bool IsStickerGiveActive => stickerGiveEnabled;

    public void BeginStickerGive()
    {
        stickerGiveEnabled = true;
    }

    public void EndStickerGive()
    {
        stickerGiveEnabled = false;
        nextStickerToBear = false;
        if (stickerGlow != null)
            stickerGlow.enabled = false;
        if (rewardStyle && !hudStickerAttaching)
            StopRewardReveal();
    }

    // Only SHOW the sticker (it pops up in front of the nurse, but cannot be given yet): the
    // nurse takes it out and asks Yaya if she likes it.
    public void ShowStickerOffer()
    {
        stickerShowOnly = true;
        stickerTemporarilyHiddenForQuiz = false;
    }

    public void HideStickerOffer()
    {
        stickerShowOnly = false;
        if (!stickerGiveEnabled && !hudStickerAttaching)
        {
            StopRewardReveal();
            if (stickerObject != null)
                stickerObject.SetActive(false);
            stickerHudVisible = false;
        }
    }
    private bool stickerShowOnly;

    // The next sticker goes on the teddy bear (e.g. "praise the bear and give it a sticker").
    // Otherwise it goes on Yaya's shirt.
    public void GiveNextStickerToBear()
    {
        nextStickerToBear = true;
    }

    // ------------------------------------------------------------------------------
    // Sticker as a game reward: hidden until a step asks for it, then it pops up in front of
    // the nurse (bounce, glow, turning rays, twinkling stars, a hint below). Clicking it (or
    // the step being done by voice) makes it fly onto Yaya's shirt / the bear with a sparkle.
    // ------------------------------------------------------------------------------
    [Header("Sticker reward (game style)")]
    [Tooltip("On: the sticker is not kept in the corner. When a step asks for it, it pops up in front of the nurse and is clicked from there.")]
    [SerializeField] private bool rewardStyle = true;
    [Tooltip("Where it pops up: x right, y up, z forward from the eyes (m).")]
    [SerializeField] private Vector3 rewardRevealOffset = new Vector3(0f, -0.10f, 0.85f);
    [Tooltip("Height of the sticker while it is offered (m).")]
    [SerializeField] private float rewardRevealHeight = 0.13f;
    [Tooltip("Small line under the offered sticker ('click to give it'). Off: the glow alone says it can be clicked.")]
    [SerializeField] private bool showRewardHint = false;
    [SerializeField] private string rewardHintForYaya = "\u9EDE\u4E00\u4E0B\uFF0C\u628A\u8CBC\u7D19\u9001\u7D66\u82BD\u82BD";
    [SerializeField] private string rewardHintForBear = "\u9EDE\u4E00\u4E0B\uFF0C\u628A\u8CBC\u7D19\u9001\u7D66\u5C0F\u718A";
    [Tooltip("Old landing animation prefab (Bear_Sticker). It put a stray grey triangle in the room, so it is off.")]
    [SerializeField] private bool useStickerAttachAnimationPrefab = false;

    [Header("Where the sticker lands")]
    [Tooltip("On Yaya's shirt: from her chest bone, forward (m) to the shirt surface.")]
    [SerializeField] private float yayaStickerForward = 0.075f;
    [Tooltip("...up (m) from the chest bone (negative = lower, on the shirt below the neckline).")]
    [SerializeField] private float yayaStickerUpOnShirt = -0.06f;
    [Tooltip("...to the side (m); the first sticker goes on her left, the next on her right.")]
    [SerializeField] private float yayaStickerSide = 0.045f;
    [SerializeField] private float yayaStickerSize = 0.055f;
    [SerializeField] private float bearStickerSize = 0.05f;

    private bool nextStickerToBear;
    private float rewardRevealStart = -1f;
    private ItemPresentFx rewardFx;
    private TextMeshPro rewardHintText;
    private int stickersOnYaya;
    private int stickersOnBear;

    private void UpdateRewardSticker()
    {
        if (stickerObject == null || hudStickerAttaching)
        {
            if (rewardHintText != null)
                rewardHintText.gameObject.SetActive(false);
            return;
        }

        bool want = (stickerGiveEnabled || stickerShowOnly) && !hudStickerWasSelected && !stickerTemporarilyHiddenForQuiz;
        if (!want)
        {
            if (stickerObject.activeSelf)
                stickerObject.SetActive(false);
            stickerHudVisible = false;
            StopRewardReveal();
            return;
        }

        if (rewardRevealStart < 0f)
            StartRewardReveal();

        // Glow / rays / stars only while the sticker can really be given. When it is only
        // shown ("do you like this sticker?") it stays plain, so it does not look clickable.
        if (stickerGiveEnabled && rewardFx == null && stickerObject.activeInHierarchy)
            rewardFx = ItemPresentFx.Attach(stickerObject.transform, rewardRevealHeight * 0.6f, ItemPresentFx.Gold, true, 18);
        else if (!stickerGiveEnabled && rewardFx != null)
        {
            rewardFx.Stop();
            rewardFx = null;
        }
        PlaceRewardSticker(Time.time - rewardRevealStart);
    }

    private void StartRewardReveal()
    {
        ResolveReferences();
        if (stickerObject == null)
            return;
        rewardRevealStart = Time.time;
        stickerHudVisible = true;
        stickerObject.SetActive(true);
        // Collider sized at full size (not at the start of the pop, when the sticker is tiny).
        PlaceRewardSticker(10f);
        EnsureHudStickerInteractable();
        PlaceRewardSticker(0f);
        PlayOneShot(popClip);
        if (rewardFx != null)
            rewardFx.Stop();
        rewardFx = stickerGiveEnabled
            ? ItemPresentFx.Attach(stickerObject.transform, rewardRevealHeight * 0.6f, ItemPresentFx.Gold, true, 18)
            : null;

        if (!showRewardHint)
        {
            if (rewardHintText != null)
                rewardHintText.gameObject.SetActive(false);
        }
        else if (rewardHintText == null)
        {
            GameObject go = new GameObject("Sticker_RewardHint");
            go.transform.SetParent(transform, false);
            rewardHintText = go.AddComponent<TextMeshPro>();
            TMP_FontAsset font = FindChineseFont();
            if (font != null)
                rewardHintText.font = font;
            rewardHintText.alignment = TextAlignmentOptions.Center;
            rewardHintText.enableWordWrapping = false;
            rewardHintText.fontStyle = FontStyles.Bold;
            rewardHintText.outlineWidth = 0.25f;
            rewardHintText.outlineColor = new Color32(90, 60, 20, 255);
            rewardHintText.sortingOrder = 30;
        }
        if (showRewardHint && stickerGiveEnabled && rewardHintText != null)
        {
            rewardHintText.text = nextStickerToBear ? rewardHintForBear : rewardHintForYaya;
            rewardHintText.gameObject.SetActive(true);
        }
        Debug.Log($"[Part3VisualDemoController] Sticker offered (goes to {(nextStickerToBear ? "the bear" : "Yaya")}).", this);
    }

    private void StopRewardReveal()
    {
        rewardRevealStart = -1f;
        if (rewardFx != null)
        {
            rewardFx.Stop();
            rewardFx = null;
        }
        if (rewardHintText != null)
            rewardHintText.gameObject.SetActive(false);
    }

    private void PlaceRewardSticker(float age)
    {
        Camera cam = Camera.main;
        if (cam == null || stickerObject == null)
            return;
        Transform c = cam.transform;
        Vector3 offset = HudDepth.Flatten(rewardRevealOffset, out float depthFactor);
        float pop = EaseOutBack(Mathf.Clamp01(age / 0.45f));
        // Bobbing / wobbling says "pick me": only while it can be given.
        float motion = stickerGiveEnabled ? 1f : 0f;
        float bob = Mathf.Sin(age * 2.4f) * 0.006f * motion;
        Vector3 position = c.position + c.right * offset.x + c.up * (offset.y + bob) + c.forward * offset.z;
        stickerObject.transform.SetPositionAndRotation(position, c.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(age * 1.7f) * 5f * motion));
        SetStickerWorldHeight(stickerObject.transform, rewardRevealHeight * depthFactor * Mathf.Max(0.02f, pop));

        if (rewardHintText != null && rewardHintText.gameObject.activeSelf)
        {
            Transform h = rewardHintText.transform;
            h.SetPositionAndRotation(position - c.up * (rewardRevealHeight * 0.78f * depthFactor), c.rotation);
            float pulse = 1f + 0.04f * Mathf.Sin(age * 4f);
            h.localScale = Vector3.one * 0.0045f * depthFactor * pulse;
            rewardHintText.fontSize = 8f;
            Color col = new Color(1f, 0.95f, 0.75f, Mathf.Clamp01((age - 0.35f) / 0.3f));
            rewardHintText.color = col;
        }
    }

    private static TMP_FontAsset FindChineseFont()
    {
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas == null || !canvas.name.Contains("Instruction_Canvas"))
                continue;
            TMP_Text text = canvas.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null)
                return text.font;
        }
        TMP_Text any = FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }

    // Where the sticker goes: Yaya's shirt (on her chest) or the bear's tummy. Returns the
    // object it is stuck to (so it moves with it), its position and rotation.
    private bool GetStickerLanding(bool onBear, out Transform parent, out Vector3 position, out Quaternion rotation, out float size)
    {
        parent = null;
        position = Vector3.zero;
        rotation = Quaternion.identity;
        size = onBear ? bearStickerSize : yayaStickerSize;
        YayaAnimationPlayer yaya = FindObjectOfType<YayaAnimationPlayer>();
        Camera viewer = Camera.main;

        if (!onBear)
        {
            if (yaya == null)
                return false;
            Transform chest = MixamoHumanoidAvatarBuilder.FindBone(yaya.transform, "Spine2")
                ?? MixamoHumanoidAvatarBuilder.FindBone(yaya.transform, "Spine1");
            if (chest == null)
                return false;
            Vector3 forward = PrefabPerformanceRuntime.GetActorPelvisForward(yaya.transform);
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = yaya.transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float side = (stickersOnYaya % 2 == 0 ? -1f : 1f) * yayaStickerSide;   // her left first
            position = chest.position + forward * yayaStickerForward + Vector3.up * yayaStickerUpOnShirt + right * side;
            // On the real surface of her shirt when the mesh can be read (Read/Write on the model).
            Vector3 around = chest.position + Vector3.up * yayaStickerUpOnShirt + right * side;
            float bestOut = float.MinValue;
            foreach (SkinnedMeshRenderer part in yaya.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (part == null || !part.enabled || !part.bounds.Contains(around))
                    continue;
                if (TryFindFrontSurface(part, around, forward, 0.025f, out Vector3 shirtPoint))
                {
                    float o = Vector3.Dot(shirtPoint - around, forward);
                    if (o > bestOut && o > 0f && o < 0.2f)
                    {
                        bestOut = o;
                        position = shirtPoint + forward * 0.004f;
                    }
                }
            }
            // Facing out of her chest (a sprite is seen from the side its forward points away from).
            rotation = Quaternion.LookRotation(-forward, Vector3.up);
            parent = chest;
            return true;
        }

        Transform bear = yaya != null ? yaya.FindComfortBear() : FindFirstNamedTransform(yayaStickerAttachTargetNames);
        if (bear == null)
            return false;
        Renderer body = null;
        float biggest = 0f;
        Vector3 reference = yaya != null ? yaya.transform.position : bear.position;
        foreach (Renderer r in bear.GetComponentsInChildren<Renderer>())
        {
            if (r is SpriteRenderer || r is LineRenderer || r is ParticleSystemRenderer
                || r.name.StartsWith("Part9_") || r.name.StartsWith("Sticker"))
                continue;
            Vector3 e = r.bounds.size;
            float score = e.x * e.y * e.z / (1f + Vector3.Distance(r.bounds.center, reference));
            if (score > biggest)
            {
                biggest = score;
                body = r;
            }
        }
        if (body == null)
            return false;

        Bounds b = body.bounds;
        Vector3 outward = viewer != null ? Vector3.ProjectOnPlane(viewer.transform.position - b.center, Vector3.up) : Vector3.forward;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;
        // Tummy height, then the frontmost point of the mesh there (its real surface).
        Vector3 tummy = b.center - Vector3.up * (b.extents.y * stickerOnBearDrop);
        position = tummy + outward * (Mathf.Abs(outward.x) * b.extents.x + Mathf.Abs(outward.z) * b.extents.z) * stickerOnBearOut;
        if (TryFindFrontSurface(body, tummy, outward, b.extents.y * 0.18f, out Vector3 surface))
            position = surface + outward * 0.004f;
        rotation = Quaternion.LookRotation(-outward, Vector3.up);
        parent = body.transform;
        return true;
    }

    // The point of the mesh furthest toward `outward` within a band around `around` (needs
    // Read/Write on the model; returns false otherwise).
    private static bool TryFindFrontSurface(Renderer body, Vector3 around, Vector3 outward, float band, out Vector3 point)
    {
        point = around;
        Mesh mesh = null;
        if (body is SkinnedMeshRenderer skinned && skinned.sharedMesh != null && skinned.sharedMesh.isReadable)
        {
            mesh = new Mesh();
            skinned.BakeMesh(mesh);
        }
        else
        {
            MeshFilter filter = body.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                mesh = filter.sharedMesh;
        }
        if (mesh == null)
            return false;

        Matrix4x4 toWorld = body is SkinnedMeshRenderer
            ? Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one)
            : body.transform.localToWorldMatrix;
        Vector3 across = Vector3.Cross(Vector3.up, outward).normalized;
        float best = float.MinValue;
        bool found = false;
        foreach (Vector3 v in mesh.vertices)
        {
            Vector3 w = toWorld.MultiplyPoint3x4(v);
            Vector3 d = w - around;
            if (Mathf.Abs(d.y) > band || Mathf.Abs(Vector3.Dot(d, across)) > band)
                continue;
            float out1 = Vector3.Dot(d, outward);
            if (out1 > best)
            {
                best = out1;
                point = w;
                found = true;
            }
        }
        return found;
    }

    private IEnumerator SquashRoutine(Transform t, Vector3 finalScale)
    {
        for (float time = 0f; time < 0.22f && t != null; time += Time.deltaTime)
        {
            float k = time / 0.22f;
            float s = 1f + 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k);
            t.localScale = new Vector3(finalScale.x * s, finalScale.y / s, finalScale.z);
            yield return null;
        }
        if (t != null)
            t.localScale = finalScale;
    }

    private void UpdateStickerGlow()
    {
        bool glowing = stickerGiveEnabled && !hudStickerWasSelected && stickerHudVisible && !hudStickerAttaching
            && stickerObject != null && stickerObject.activeInHierarchy;

        if (glowing && stickerGlow == null)
        {
            SpriteRenderer source = stickerObject.GetComponent<SpriteRenderer>();
            if (source != null && source.sprite != null)
            {
                GameObject glowObject = new GameObject("Sticker_Glow");
                glowObject.transform.SetParent(stickerObject.transform, false);
                glowObject.transform.localPosition = new Vector3(0f, 0f, 0.002f);
                stickerGlow = glowObject.AddComponent<SpriteRenderer>();
                stickerGlow.sprite = source.sprite;
                stickerGlow.sortingLayerID = source.sortingLayerID;
                stickerGlow.sortingOrder = source.sortingOrder - 1;
            }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * stickerGlowPulseSpeed);
        if (stickerGlow != null)
        {
            stickerGlow.enabled = glowing;
            if (glowing)
            {
                Color c = stickerGlowColor;
                c.a = Mathf.Lerp(0.35f, 0.9f, pulse);
                stickerGlow.color = c;
                stickerGlow.transform.localScale = Vector3.one * Mathf.Lerp(1.14f, 1.22f, pulse);
            }
        }

        // Gentle "pick me" pulse of the sticker itself (placement resets the scale every frame).
        if (glowing)
            stickerObject.transform.localScale *= 1f + 0.015f * pulse;
    }

    // VR: pulling the trigger while a controller ray points at the sticker gives it.
    // (Done by hand because the sticker's collider is a trigger, which controller rays skip.)
    private void UpdateVrStickerClick()
    {
        bool down = false;
        System.Collections.Generic.List<UnityEngine.XR.InputDevice> devices = xrDevices;
        devices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand, devices);
        foreach (UnityEngine.XR.InputDevice device in devices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed)
                down = true;
        }

        bool clicked = down && !vrTriggerWasDown;
        vrTriggerWasDown = down;
        if (!clicked || !stickerGiveEnabled || !stickerHudVisible || hudStickerAttaching || stickerObject == null || !stickerObject.activeInHierarchy)
            return;

        if (rayInteractors == null || Time.unscaledTime >= nextRayScanTime)
        {
            rayInteractors = FindObjectsOfType<XRRayInteractor>();
            nextRayScanTime = Time.unscaledTime + 2f;
        }

        Transform sticker = stickerObject.transform;
        float half = Mathf.Max(hudStickerColliderWorldSize.x, hudStickerColliderWorldSize.y, fallbackStickerWorldHeight) * 0.5f + 0.03f;
        Plane plane = new Plane(sticker.forward, sticker.position);
        foreach (XRRayInteractor interactor in rayInteractors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled)
                continue;

            Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
            if (!plane.Raycast(ray, out float distance) || distance > 6f)
                continue;

            Vector3 local = sticker.InverseTransformDirection(ray.GetPoint(distance) - sticker.position);
            if (Mathf.Abs(local.x) <= half && Mathf.Abs(local.y) <= half)
            {
                HandleHudStickerClicked();
                return;
            }
        }
    }

    // Left column: same x as the medical-record button, below it, clear of prompt/dialogue boxes.
    private bool TryGetStickerColumnOffset(Transform cameraTransform, out Vector3 offset)
    {
        offset = stickerColumnOffset;
        if (stickerColumnSolved && Time.unscaledTime - stickerColumnSolveTime < 3f)
            return true;

        if (!HudFreeSpot.TryGetRecordButtonRect(cameraTransform, out Rect button))
            return stickerColumnSolved;

        float z = Mathf.Max(0.3f, fallbackStickerCameraOffset.z);
        float half = Mathf.Max(0.01f, fallbackStickerWorldHeight * hudStickerSizeMultiplier) * 0.5f / z;
        const float margin = 0.006f;
        float x = button.center.x;

        // Always right under the record button (same column).
        float y = button.yMin - margin - half;

        stickerColumnOffset = new Vector3(x * z, y * z + stickerRaise, z);
        if (!stickerColumnSolved)
            Debug.Log($"[Part3VisualDemoController] Sticker HUD spot: x={stickerColumnOffset.x:0.000} y={stickerColumnOffset.y:0.000} z={z:0.00} (record button x={button.center.x * z:0.000})", this);
        stickerColumnSolved = true;
        stickerColumnSolveTime = Time.unscaledTime;
        offset = stickerColumnOffset;
        return true;
    }
    private void ResolveReferences()
    {
        NormalizeStickerHudOffset();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (popClip == null)
            popClip = Resources.Load<AudioClip>("pop") ?? FindAudioClipByName("pop");

        if (heartbeatClip == null)
            heartbeatClip = Resources.Load<AudioClip>("Heartbeat-sound")
                ?? Resources.Load<AudioClip>("heartbeat-sound")
                ?? FindAudioClipByName("Heartbeat-sound")
                ?? FindAudioClipByName("heartbeat-sound")
                ?? FindAudioClipByName("heartbeat");
        PreferSpriteSticker();


        if (stickerObject == null)
            stickerObject = FindSceneObjectByName("Sticker");

        if (stickerObject == null)
            stickerObject = CreateSpriteSticker(FindSpriteByName("bear_sticker"));

        if (stickerObject == null)
            stickerObject = CreateStickerFromExistingBear();

        if (stickerObject == null)
            stickerObject = CreateFallbackSticker();

        EnsureStickerSceneInstance();

        // Scene sticker objects can carry an old oversized scale. Normalize the sticker
        // before it is shown so the reward stays the same size in every run.
        ApplyStickerHudScale();

        if (stickerAnchor == null)
            stickerAnchor = FindSceneObjectByName("Sticker_Anchor")?.transform;

        if (stethoscope == null)
            stethoscope = FindSceneObjectByName("Stethoscope")?.transform
                ?? FindSceneObjectByName("Stethoscope (1)")?.transform;

        if (nurseHandAnchor == null)
            nurseHandAnchor = FindSceneObjectByName("Nurse_Hand_Anchor")?.transform;

        if (yayaHandAnchor == null)
            yayaHandAnchor = FindSceneObjectByName("Yaya_Hand_Anchor")?.transform;

        if (momHandAnchor == null)
            momHandAnchor = FindSceneObjectByName("Mom_Hand_Anchor")?.transform;

        if (momChestAnchor == null)
            momChestAnchor = FindSceneObjectByName("Mom_Chest_StethoscopeAnchor")?.transform
                ?? FindSceneObjectByName("Mom_Chest_Anchor")?.transform;

        if (yayaChestAnchor == null)
            yayaChestAnchor = FindSceneObjectByName("Yaya_Chest_StethoscopeAnchor")?.transform
                ?? FindSceneObjectByName("Yaya_Chest_Anchor")?.transform;

        if (yayaStickerAttachTarget == null)
            yayaStickerAttachTarget = FindFirstNamedTransform(yayaStickerAttachTargetNames) ?? yayaChestAnchor;
    }




    private void PreferSpriteSticker()
    {
        if (stickerSprite == null)
            stickerSprite = FindSpriteByName("bear_sticker") ?? FindSpriteByName("bear");

        if (stickerSprite == null)
            return;

        if (stickerObject != null && stickerObject.scene.IsValid() && stickerObject.GetComponent<SpriteRenderer>() != null)
            return;

        stickerObject = CreateSpriteSticker(stickerSprite);
    }

    private void EnsureStickerSceneInstance()
    {
        if (stickerObject == null)
            return;

        if (stickerObject.scene.IsValid())
            return;

        stickerPrefabSource = stickerObject;
        stickerObject = Instantiate(stickerPrefabSource, transform);
        stickerObject.name = stickerPrefabSource.name + "_Runtime";
        stickerObject.SetActive(false);
    }

    private void EnsureHudStickerInteractable()
    {
        if (stickerObject == null)
            return;

        ConfigureStickerCollider(stickerObject, hudStickerColliderWorldSize);

        hudStickerInteractable = stickerObject.GetComponent<XRSimpleInteractable>();
        if (hudStickerInteractable == null)
            hudStickerInteractable = stickerObject.AddComponent<XRSimpleInteractable>();

        StickerHudClickProxy clickProxy = stickerObject.GetComponent<StickerHudClickProxy>();
        if (clickProxy == null)
            clickProxy = stickerObject.AddComponent<StickerHudClickProxy>();
        clickProxy.Setup(this);

        hudStickerInteractable.enabled = true;
        hudStickerInteractable.selectEntered.RemoveListener(OnHudStickerSelected);
        hudStickerInteractable.selectEntered.AddListener(OnHudStickerSelected);
    }

    internal void HandleHudStickerClicked()
    {
        OnHudStickerSelected(null);
    }

    // From the sticker's own OnMouseDown (legacy input): ignored when the mouse click was
    // already handled this frame by UpdateMouseStickerClick.
    internal void HandleHudStickerClickedByMouseDown()
    {
        if (Time.frameCount == lastMouseStickerFrame)
            return;
        lastMouseStickerFrame = Time.frameCount;
        HandleHudStickerClicked();
    }
    private void OnHudStickerSelected(SelectEnterEventArgs args)
    {
        if (hudStickerAttaching)
            return;

        // The sticker can only be given while a prompt asks for it, and only once per prompt.
        if (!stickerGiveEnabled || hudStickerWasSelected)
            return;

        hudStickerWasSelected = true;
        // A sticker makes Yaya feel a little better.
        KidEmotionPresenter.Calm(stickerCalmAmount, "\u8CBC\u7D19\u9F13\u52F5");
        stickerHudVisible = false;
        if (stickerObject != null)
            stickerObject.SetActive(true);

        onHudStickerSelected?.Invoke();
        PlayHudStickerAttachAnimation();
    }

    private void PlayHudStickerAttachAnimation()
    {
        ResolveReferences();
        if (rewardStyle)
        {
            if (rewardFx != null)
            {
                rewardFx.Stop();
                rewardFx = null;
            }
            if (rewardHintText != null)
                rewardHintText.gameObject.SetActive(false);
            if (hudStickerAttachRoutine != null)
                StopCoroutine(hudStickerAttachRoutine);
            hudStickerAttachRoutine = StartCoroutine(FlyStickerToRecipientRoutine(nextStickerToBear));
            return;
        }
        Transform animationTarget = FindFirstNamedTransform(yayaStickerAttachTargetNames) ?? yayaStickerAttachTarget;

        // Always the teddy that is next to Yaya, and its body itself (the named object can be a
        // group of several bears, whose middle is somewhere between them).
        YayaAnimationPlayer yayaPlayer = FindObjectOfType<YayaAnimationPlayer>();
        Transform nearBear = yayaPlayer != null ? yayaPlayer.FindComfortBear() : null;
        if (nearBear != null)
        {
            Renderer body = null;
            float biggest = 0f;
            Vector3 reference = yayaPlayer.transform.position;
            foreach (Renderer r in nearBear.GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer || r is LineRenderer || r is ParticleSystemRenderer
                    || r.name.StartsWith("Part9_") || r.name.StartsWith("Sticker"))
                    continue;
                Vector3 e = r.bounds.size;
                // biggest mesh, preferring the one closest to Yaya when there are several bears
                float score = e.x * e.y * e.z / (1f + Vector3.Distance(r.bounds.center, reference));
                if (score > biggest)
                {
                    biggest = score;
                    body = r;
                }
            }
            if (body != null)
                animationTarget = body.transform;
        }

        if (hudStickerAttachRoutine != null)
            StopCoroutine(hudStickerAttachRoutine);

        hudStickerAttachRoutine = StartCoroutine(FlyHudStickerToTargetRoutine(animationTarget));

        if (useStickerAttachAnimationPrefab && stickerAttachAnimationPrefab == null)
            stickerAttachAnimationPrefab = FindStickerAttachAnimationPrefab();

        if (useStickerAttachAnimationPrefab && stickerAttachAnimationPrefab != null && animationTarget != null)
        {
            GetStickerTargetPose(animationTarget, out Vector3 animationPosition, out Quaternion animationRotation);
            GameObject animationObject = Instantiate(stickerAttachAnimationPrefab, animationPosition, animationRotation);
            animationObject.name = stickerAttachAnimationPrefab.name + "_HudStickerAttach";

            Animator animator = animationObject.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                if (!string.IsNullOrWhiteSpace(stickerAttachAnimationState))
                    animator.Play(stickerAttachAnimationState, 0, 0f);
                else if (animator.HasState(0, Animator.StringToHash("Scene")))
                    animator.Play("Scene", 0, 0f);
                else if (animator.HasState(0, Animator.StringToHash("Bear_Sticker")))
                    animator.Play("Bear_Sticker", 0, 0f);
            }

            Destroy(animationObject, stickerAttachAnimationDuration);
        }
    }


    private GameObject FindStickerAttachAnimationPrefab()
    {
#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(stickerAttachAnimationPrefabNames))
            return null;

        string[] names = stickerAttachAnimationPrefabNames.Split('|');
        foreach (string rawName in names)
        {
            string wantedName = rawName.Trim();
            if (string.IsNullOrEmpty(wantedName))
                continue;

            string[] guids = AssetDatabase.FindAssets($"{wantedName} t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.name.IndexOf(wantedName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return prefab;
            }
        }
#endif
        return null;
    }
    // Game style: the offered sticker flies in an arc onto Yaya's shirt / the bear's tummy,
    // shrinking to sticker size and turning flat onto it, then lands with a squash and sparkle.
    // A copy stays there (moving with her / the bear); the offered one is hidden again.
    private IEnumerator FlyStickerToRecipientRoutine(bool onBear)
    {
        if (stickerObject == null)
            yield break;

        hudStickerAttaching = true;
        stickerObject.SetActive(true);
        if (hudStickerInteractable != null)
            hudStickerInteractable.enabled = false;
        SetCollidersEnabled(stickerObject, false);
        PlayOneShot(popClip);

        Vector3 startPosition = stickerObject.transform.position;
        Quaternion startRotation = stickerObject.transform.rotation;
        Vector3 startScale = stickerObject.transform.localScale;
        bool hasLanding = GetStickerLanding(onBear, out Transform parent, out Vector3 targetPosition, out Quaternion targetRotation, out float size);
        if (!hasLanding)
        {
            targetPosition = startPosition;
            targetRotation = startRotation;
        }

        // Little "wind-up" pop, then the flight.
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            stickerObject.transform.localScale = startScale * (1f + 0.15f * Mathf.Sin(t / 0.12f * Mathf.PI));
            yield return null;
        }

        float duration = 0.55f;
        SetStickerWorldHeight(stickerObject.transform, size);
        Vector3 endScale = stickerObject.transform.localScale;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float e = t * t * (3f - 2f * t);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.15f;
            stickerObject.transform.position = Vector3.Lerp(startPosition, targetPosition, e) + Vector3.up * arc;
            stickerObject.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, e) * Quaternion.Euler(0f, 0f, (1f - e) * 360f);
            stickerObject.transform.localScale = Vector3.Lerp(startScale, endScale, e);
            yield return null;
        }

        if (hasLanding)
        {
            GameObject copy = Instantiate(stickerObject, targetPosition, targetRotation);
            copy.name = onBear ? "Sticker_OnBear" : "Sticker_OnYaya";
            foreach (Renderer copyRenderer in copy.GetComponentsInChildren<Renderer>(true))
                copyRenderer.sortingOrder = -20;
            XRSimpleInteractable copyInteractable = copy.GetComponent<XRSimpleInteractable>();
            if (copyInteractable != null)
                Destroy(copyInteractable);
            StickerHudClickProxy copyProxy = copy.GetComponent<StickerHudClickProxy>();
            if (copyProxy != null)
                Destroy(copyProxy);
            foreach (Collider copyCollider in copy.GetComponentsInChildren<Collider>(true))
                Destroy(copyCollider);
            Transform copyGlow = copy.transform.Find("Sticker_Glow");
            if (copyGlow != null)
                Destroy(copyGlow.gameObject);
            SetStickerWorldHeight(copy.transform, size);
            if (onBear)
                copy.transform.SetParent(parent, true);
            else
            {
                // Not a child of Yaya: when Yaya is swapped for an animated copy (listening with
                // the stethoscope) her own body is hidden, and the sticker went with it. It
                // follows the same bone on whichever Yaya is visible instead.
                YayaAnimationPlayer owner = FindObjectOfType<YayaAnimationPlayer>();
                copy.AddComponent<StickerBoneFollower>().Setup(parent, owner != null ? owner.transform : parent.root);
            }
            StartCoroutine(SquashRoutine(copy.transform, copy.transform.localScale));
            ItemPresentFx.Burst(targetPosition, size * 1.6f, ItemPresentFx.Gold);
            if (onBear) stickersOnBear++; else stickersOnYaya++;
            placedStickerCopy = copy;
            Debug.Log($"[Part3VisualDemoController] Sticker stuck on {(onBear ? "the bear" : "Yaya's shirt")} at {targetPosition:F3}.", copy);
        }

        stickerObject.SetActive(false);
        stickerObject.transform.localScale = startScale;
        SetCollidersEnabled(stickerObject, true);
        if (hudStickerInteractable != null)
            hudStickerInteractable.enabled = true;
        stickerHudVisible = false;
        hudStickerAttaching = false;
        hudStickerAttachRoutine = null;
        StopRewardReveal();
    }

    private IEnumerator FlyHudStickerToTargetRoutine(Transform target)
    {
        if (stickerObject == null)
            yield break;

        hudStickerAttaching = true;
        stickerHudVisible = false;
        stickerObject.SetActive(true);

        if (hudStickerInteractable != null)
            hudStickerInteractable.enabled = false;

        SetCollidersEnabled(stickerObject, false);
        PlayOneShot(popClip);

        Vector3 startPosition = stickerObject.transform.position;
        Quaternion startRotation = stickerObject.transform.rotation;
        Vector3 startScale = stickerObject.transform.localScale;
        Vector3 targetPosition = startPosition;
        Quaternion targetRotation = startRotation;
        if (target != null)
        {
            GetStickerTargetPose(target, out targetPosition, out targetRotation);
            // On the side of the bear that faces the nurse, turned toward her.
            Camera viewer = Camera.main;
            Renderer body = target.GetComponent<Renderer>();
            if (viewer != null && body != null)
            {
                // Flat on the bear's tummy: at tummy height, on its surface (about half-way out
                // from its middle, the rest of its depth being the legs), upright and facing out.
                Bounds b = body.bounds;
                Vector3 outward = Vector3.ProjectOnPlane(viewer.transform.position - b.center, Vector3.up);
                if (outward.sqrMagnitude > 1e-4f)
                {
                    outward.Normalize();
                    float halfDepth = Mathf.Abs(outward.x) * b.extents.x + Mathf.Abs(outward.z) * b.extents.z;
                    targetPosition = b.center - Vector3.up * (b.extents.y * stickerOnBearDrop) + outward * (halfDepth * stickerOnBearOut);
                    targetRotation = Quaternion.LookRotation(-outward, Vector3.up);
                }
            }
            else if (viewer != null)
            {
                targetRotation = viewer.transform.rotation;
            }
        }

        float duration = Mathf.Max(0.25f, stickerAttachAnimationDuration * 0.45f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.18f;

            stickerObject.transform.position = Vector3.Lerp(startPosition, targetPosition, t) + Vector3.up * arc;
            stickerObject.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            stickerObject.transform.localScale = Vector3.Lerp(startScale, startScale * 0.82f, t);
            yield return null;
        }

        if (target != null)
        {
            // A copy of the sticker stays on the bear...
            if (placedStickerCopy != null)
                Destroy(placedStickerCopy);
            placedStickerCopy = Instantiate(stickerObject, targetPosition, targetRotation);
            placedStickerCopy.name = "Sticker_OnBear";
            // The corner sticker is drawn on top of everything (it is part of the HUD). The copy
            // on the bear is a thing in the room: panels in front of it must cover it.
            foreach (Renderer copyRenderer in placedStickerCopy.GetComponentsInChildren<Renderer>(true))
                copyRenderer.sortingOrder = -20;
            XRSimpleInteractable copyInteractable = placedStickerCopy.GetComponent<XRSimpleInteractable>();
            if (copyInteractable != null)
                Destroy(copyInteractable);
            StickerHudClickProxy copyProxy = placedStickerCopy.GetComponent<StickerHudClickProxy>();
            if (copyProxy != null)
                Destroy(copyProxy);
            foreach (Collider copyCollider in placedStickerCopy.GetComponentsInChildren<Collider>(true))
                Destroy(copyCollider);
            Transform copyGlow = placedStickerCopy.transform.Find("Sticker_Glow");
            if (copyGlow != null)
                Destroy(copyGlow.gameObject);
            // Size it first (in world units), then attach: its size no longer depends on
            // the scale of whatever it is attached to.
            SetStickerWorldHeight(placedStickerCopy.transform, placedStickerWorldHeight);
            placedStickerCopy.transform.SetParent(target, true);
        }

        // ...and the sticker itself goes back to its corner, ready for the next time it is needed.
        stickerObject.transform.localScale = startScale;
        SetCollidersEnabled(stickerObject, true);
        if (hudStickerInteractable != null)
            hudStickerInteractable.enabled = true;
        stickerHudVisible = true;
        hudStickerAttaching = false;
        hudStickerAttachRoutine = null;
    }
    private static void GetStickerTargetPose(Transform target, out Vector3 position, out Quaternion rotation)
    {
        position = target != null ? target.position : Vector3.zero;
        rotation = target != null ? target.rotation : Quaternion.identity;

        if (target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            position = bounds.center - Vector3.up * (bounds.extents.y * 0.30f);   // on its tummy, not its face
    }
    private void SpawnPlaceableSticker(Transform interactorTransform)
    {
        ResolveReferences();
        if (stickerObject == null)
            return;

        GameObject copy = Instantiate(stickerObject);
        copy.name = "Part3_PlaceableSticker";
        copy.SetActive(true);

        XRSimpleInteractable simple = copy.GetComponent<XRSimpleInteractable>();
        if (simple != null)
            Destroy(simple);

        if (interactorTransform != null)
            copy.transform.SetPositionAndRotation(interactorTransform.position, interactorTransform.rotation);
        else
            copy.transform.SetPositionAndRotation(stickerObject.transform.position, stickerObject.transform.rotation);

        SetStickerWorldHeight(copy.transform, placedStickerWorldHeight);
        SetCollidersEnabled(copy, true);

        ConfigureStickerCollider(copy, placedStickerColliderWorldSize);

        Rigidbody body = copy.GetComponent<Rigidbody>();
        if (body == null)
            body = copy.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;

        XRGrabInteractable grab = copy.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = copy.AddComponent<XRGrabInteractable>();
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.throwOnDetach = false;

        StickerAutoAttach attach = copy.AddComponent<StickerAutoAttach>();
        attach.Setup(yayaStickerAttachTarget, stickerAttachDistance, placedStickerWorldHeight, audioSource, popClip, stickerAttachAnimationPrefab, stickerAttachAnimationState, stickerAttachAnimationDuration);
        PlayOneShot(popClip);
    }

    private void SetStickerWorldHeight(Transform target, float worldHeight)
    {
        if (target == null)
            return;

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float spriteHeight = Mathf.Max(0.001f, spriteRenderer.sprite.bounds.size.y);
            float scale = Mathf.Max(0.001f, worldHeight) / spriteHeight;
            target.localScale = new Vector3(scale, scale, 1f);
            return;
        }

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.bounds.size.y > 0.001f)
        {
            float ratio = Mathf.Max(0.001f, worldHeight) / renderer.bounds.size.y;
            target.localScale *= ratio;
        }
    }

    private static void ConfigureStickerCollider(GameObject sticker, Vector2 minimumWorldSize)
    {
        if (sticker == null)
            return;

        BoxCollider box = sticker.GetComponent<BoxCollider>();
        if (box == null)
            box = sticker.AddComponent<BoxCollider>();

        box.enabled = true;
        box.isTrigger = true;

        Vector3 localSize = Vector3.one;
        SpriteRenderer spriteRenderer = sticker.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            localSize = spriteRenderer.sprite.bounds.size;

        Vector3 lossyScale = sticker.transform.lossyScale;
        float minLocalX = Mathf.Max(0.001f, minimumWorldSize.x) / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x));
        float minLocalY = Mathf.Max(0.001f, minimumWorldSize.y) / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y));

        localSize.x = Mathf.Max(Mathf.Abs(localSize.x), minLocalX);
        localSize.y = Mathf.Max(Mathf.Abs(localSize.y), minLocalY);
        localSize.z = Mathf.Max(Mathf.Abs(localSize.z), 0.02f);

        box.center = Vector3.zero;
        box.size = localSize;
    }
    private GameObject CreateFallbackSticker()
    {
        if (stickerSprite == null)
            stickerSprite = FindSpriteByName("bear_sticker") ?? FindSpriteByName("bear");

        if (stickerSprite != null)
            return CreateSpriteSticker(stickerSprite);

        GameObject sticker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sticker.name = "Part3_AutoSticker";
        sticker.transform.SetParent(transform, false);
        sticker.transform.localScale = new Vector3(fallbackStickerSize.x, fallbackStickerSize.y, 1f);

        Renderer renderer = sticker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = new Color(1f, 0.86f, 0.12f, 1f);
        }

        GameObject label = new GameObject("Sticker_Label");
        label.transform.SetParent(sticker.transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one * 0.16f;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = "Sticker";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.25f, 0.12f, 0.03f, 1f);
        textMesh.characterSize = 0.16f;

        sticker.SetActive(false);
        return sticker;
    }

    private GameObject CreateStickerFromExistingBear()
    {
        GameObject sourceBear = FindSceneObjectByName("bow_bear")
            ?? FindSceneObjectByName("bear.002")
            ?? FindSceneObjectByName("bears");

        if (sourceBear == null)
            return null;

        GameObject sticker = Instantiate(sourceBear, transform);
        sticker.name = "Part3_BearStickerObject";
        sticker.SetActive(false);
        SetCollidersEnabled(sticker, false);
        return sticker;
    }

    private GameObject CreateSpriteSticker(Sprite sprite)
    {
        if (sprite == null)
            return null;

        GameObject sticker = new GameObject("Part3_BearSticker");
        sticker.transform.SetParent(transform, false);
        sticker.transform.localScale = new Vector3(fallbackStickerSize.x, fallbackStickerSize.y, 1f);

        SpriteRenderer spriteRenderer = sticker.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 20;

        sticker.SetActive(false);
        return sticker;
    }

    private void PlaceFallbackStickerNearCamera()
    {
        if (stickerObject == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        Vector3 hudOffset = fallbackStickerCameraOffset;
        if (alignStickerWithRecordButton && TryGetStickerColumnOffset(cameraTransform, out Vector3 aligned))
            hudOffset = aligned;
        hudOffset = HudDepth.Flatten(hudOffset, out float depthFactor);

        Vector3 targetPosition = cameraTransform.position
            + cameraTransform.right * hudOffset.x
            + cameraTransform.up * hudOffset.y
            + cameraTransform.forward * hudOffset.z;

        stickerObject.transform.position = targetPosition;
        stickerObject.transform.rotation = cameraTransform.rotation;
        ApplyStickerHudScale();
        stickerObject.transform.localScale *= depthFactor;
    }


    private void ApplyStickerHudScale()
    {
        if (stickerObject == null)
            return;

        SpriteRenderer spriteRenderer = stickerObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float spriteHeight = Mathf.Max(0.001f, spriteRenderer.sprite.bounds.size.y);
            float scale = Mathf.Max(0.001f, fallbackStickerWorldHeight * hudStickerSizeMultiplier) / spriteHeight;
            stickerObject.transform.localScale = new Vector3(scale, scale, 1f);
            return;
        }

        Renderer renderer = stickerObject.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.bounds.size.y > 0.001f)
        {
            float ratio = Mathf.Max(0.001f, fallbackStickerWorldHeight * hudStickerSizeMultiplier) / renderer.bounds.size.y;
            stickerObject.transform.localScale *= ratio;
            return;
        }

        stickerObject.transform.localScale = new Vector3(fallbackStickerSize.x, fallbackStickerSize.y, 1f);
    }

    private static Transform FindFirstNamedTransform(string pipeSeparatedNames)
    {
        if (string.IsNullOrWhiteSpace(pipeSeparatedNames))
            return null;

        string[] names = pipeSeparatedNames.Split('|');
        foreach (string rawName in names)
        {
            string objectName = rawName.Trim();
            if (string.IsNullOrEmpty(objectName))
                continue;

            GameObject found = FindSceneObjectByName(objectName);
            if (found != null)
                return found.transform;
        }

        return null;
    }
    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform == null || transform.gameObject == null)
                continue;

            if (transform.name == objectName && transform.gameObject.scene.IsValid())
                return transform.gameObject;
        }

        return null;
    }

    private static AudioClip FindAudioClipByName(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;

            if (clip.name == clipName || clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
        }

        return null;
    }

    private static Sprite FindSpriteByName(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
            return null;

        Sprite resourceSprite = Resources.Load<Sprite>(spriteName);
        if (resourceSprite != null)
            return resourceSprite;

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
                continue;

            if (sprite.name == spriteName || sprite.name.IndexOf(spriteName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return sprite;
        }

        return null;
    }

    private static void SetCollidersEnabled(GameObject root, bool enabled)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            collider.enabled = enabled;
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
public class StickerAutoAttach : MonoBehaviour
{
    private Transform target;
    private float attachDistance = 0.22f;
    private float attachedWorldHeight = 0.09f;
    private AudioSource audioSource;
    private AudioClip attachClip;
    private GameObject attachAnimationPrefab;
    private string attachAnimationState;
    private float attachAnimationDuration = 1.5f;
    private bool attached;

    public void Setup(Transform target, float attachDistance, float attachedWorldHeight, AudioSource audioSource, AudioClip attachClip, GameObject attachAnimationPrefab = null, string attachAnimationState = "", float attachAnimationDuration = 1.5f)
    {
        this.target = target;
        this.attachDistance = Mathf.Max(0.01f, attachDistance);
        this.attachedWorldHeight = Mathf.Max(0.01f, attachedWorldHeight);
        this.audioSource = audioSource;
        this.attachClip = attachClip;
        this.attachAnimationPrefab = attachAnimationPrefab;
        this.attachAnimationState = attachAnimationState;
        this.attachAnimationDuration = Mathf.Max(0.1f, attachAnimationDuration);
    }

    private void Update()
    {
        if (attached || target == null)
            return;

        if (Vector3.Distance(transform.position, target.position) <= attachDistance)
            AttachToTarget();
    }

    private void AttachToTarget()
    {
        attached = true;
        transform.SetParent(target, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetWorldHeight(attachedWorldHeight);

        XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
            Destroy(grab);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
            Destroy(body);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider stickerCollider in colliders)
            stickerCollider.enabled = false;

        if (audioSource != null && attachClip != null)
            audioSource.PlayOneShot(attachClip);

        PlayAttachAnimation();
    }


    private void PlayAttachAnimation()
    {
        if (attachAnimationPrefab == null || target == null)
            return;

        GameObject animationObject = Instantiate(attachAnimationPrefab, target);
        animationObject.name = attachAnimationPrefab.name + "_Instance";
        animationObject.transform.localPosition = Vector3.zero;
        animationObject.transform.localRotation = Quaternion.identity;

        Animator animator = animationObject.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = true;
            if (!string.IsNullOrWhiteSpace(attachAnimationState))
                animator.Play(attachAnimationState, 0, 0f);
        }

        Destroy(animationObject, attachAnimationDuration);
    }

    private void SetWorldHeight(float worldHeight)
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float spriteHeight = Mathf.Max(0.001f, spriteRenderer.sprite.bounds.size.y);
            float scale = Mathf.Max(0.001f, worldHeight) / spriteHeight;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}

public class StickerHudClickProxy : MonoBehaviour
{
    private Part3VisualDemoController owner;

    public void Setup(Part3VisualDemoController controller)
    {
        owner = controller;
    }

    private void OnMouseDown()
    {
        owner?.HandleHudStickerClickedByMouseDown();
    }
}

// Keeps a sticker on a bone of Yaya (her chest). If her body is hidden because an animated
// copy of her is shown instead, it follows the bone with the same name on that copy.
public class StickerBoneFollower : MonoBehaviour
{
    private Transform bone;
    private Transform owner;
    private string boneName;
    private Vector3 offset;
    private Quaternion turn;
    private Renderer ownerBody;
    private Transform standIn;
    private float nextScan;
    private Renderer[] own;

    public void Setup(Transform followBone, Transform ownerRoot)
    {
        bone = followBone;
        owner = ownerRoot;
        boneName = followBone != null ? followBone.name : string.Empty;
        if (bone != null)
        {
            offset = Quaternion.Inverse(bone.rotation) * (transform.position - bone.position);
            turn = Quaternion.Inverse(bone.rotation) * transform.rotation;
        }
        float biggest = 0f;
        if (owner != null)
        {
            foreach (SkinnedMeshRenderer r in owner.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                float size = r.bounds.size.sqrMagnitude;
                if (size > biggest)
                {
                    biggest = size;
                    ownerBody = r;
                }
            }
        }
        own = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        if (bone == null)
        {
            Destroy(gameObject);
            return;
        }

        Transform follow = bone;
        bool ownerVisible = ownerBody == null || (ownerBody.enabled && ownerBody.gameObject.activeInHierarchy);
        if (!ownerVisible)
        {
            if ((standIn == null || !standIn.gameObject.activeInHierarchy) && Time.time >= nextScan)
            {
                nextScan = Time.time + 0.5f;
                standIn = FindStandIn();
            }
            follow = standIn;
        }

        bool show = follow != null;
        foreach (Renderer r in own)
        {
            if (r != null && r.enabled != show)
                r.enabled = show;
        }
        if (!show)
            return;
        transform.SetPositionAndRotation(follow.position + follow.rotation * offset, follow.rotation * turn);
    }

    // The visible character (not Yaya herself) that has a bone with the same name, nearest to her.
    private Transform FindStandIn()
    {
        Transform best = null;
        float bestDistance = 2f;
        foreach (SkinnedMeshRenderer r in FindObjectsOfType<SkinnedMeshRenderer>())
        {
            if (r == null || !r.enabled || (owner != null && r.transform.IsChildOf(owner)))
                continue;
            Transform found = FindDeep(r.transform.root, boneName);
            if (found == null || (owner != null && found.IsChildOf(owner)))
                continue;
            float d = Vector3.Distance(found.position, bone.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = found;
            }
        }
        return best;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDeep(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }
        return null;
    }
}
