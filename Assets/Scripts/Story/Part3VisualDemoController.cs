using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
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
    [SerializeField] private float stickerExtraVerticalOffset = 0.012f;
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

        if (stickerRoutine != null)
            StopCoroutine(stickerRoutine);

        stickerRoutine = StartCoroutine(ShowStickerRoutine());
    }

    public void HighlightSticker()
    {
        if (stickerRoutine != null)
            StopCoroutine(stickerRoutine);

        stickerRoutine = StartCoroutine(StickerPulseRoutine());
    }

    public void HideSticker()
    {
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
        if (!stickerTemporarilyHiddenForQuiz && !hudStickerAttaching && stickerHudVisible && keepStickerInCameraCorner)
            PlaceFallbackStickerNearCamera();

        UpdateStickerGlow();
        UpdateVrStickerClick();
    }

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
        if (stickerGlow != null)
            stickerGlow.enabled = false;
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

        stickerColumnOffset = new Vector3(x * z, y * z + stickerExtraVerticalOffset, z);
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
    private void OnHudStickerSelected(SelectEnterEventArgs args)
    {
        if (hudStickerAttaching)
            return;

        // The sticker can only be given while a prompt asks for it, and only once per prompt.
        if (!stickerGiveEnabled || hudStickerWasSelected)
            return;

        hudStickerWasSelected = true;
        stickerHudVisible = false;
        if (stickerObject != null)
            stickerObject.SetActive(true);

        onHudStickerSelected?.Invoke();
        PlayHudStickerAttachAnimation();
    }

    private void PlayHudStickerAttachAnimation()
    {
        ResolveReferences();
        Transform animationTarget = FindFirstNamedTransform(yayaStickerAttachTargetNames) ?? yayaStickerAttachTarget;

        if (hudStickerAttachRoutine != null)
            StopCoroutine(hudStickerAttachRoutine);

        hudStickerAttachRoutine = StartCoroutine(FlyHudStickerToTargetRoutine(animationTarget));

        if (stickerAttachAnimationPrefab == null)
            stickerAttachAnimationPrefab = FindStickerAttachAnimationPrefab();

        if (stickerAttachAnimationPrefab != null && animationTarget != null)
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
            GetStickerTargetPose(target, out targetPosition, out targetRotation);

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
            placedStickerCopy.transform.SetParent(target, true);
            SetStickerWorldHeight(placedStickerCopy.transform, placedStickerWorldHeight);
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
            position = bounds.center + Vector3.up * (bounds.extents.y * 0.2f);
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

        Vector3 targetPosition = cameraTransform.position
            + cameraTransform.right * hudOffset.x
            + cameraTransform.up * hudOffset.y
            + cameraTransform.forward * hudOffset.z;

        stickerObject.transform.position = targetPosition;
        stickerObject.transform.rotation = cameraTransform.rotation;
        ApplyStickerHudScale();
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
        owner?.HandleHudStickerClicked();
    }
}
