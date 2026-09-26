using System.Collections;
using UnityEngine;

public class Part3VisualDemoController : MonoBehaviour
{
    [Header("Sticker")]
    [SerializeField] private GameObject stickerObject;
    [SerializeField] private Sprite stickerSprite;
    [SerializeField] private Transform stickerAnchor;
    [SerializeField] private Vector3 fallbackStickerCameraOffset = new Vector3(0.38f, -0.22f, 0.85f);
    // Keep the bear sticker close to the size of the medical-record card in world space.
    [SerializeField] private Vector2 fallbackStickerSize = new Vector2(0.075f, 0.075f);
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popClip;
    [SerializeField] private float stickerPopDuration = 0.3f;
    [SerializeField] private float stickerHighlightDuration = 0.35f;

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

    private Coroutine stickerRoutine;
    private Coroutine stethoscopeRoutine;
    private Vector3 stickerBaseScale = Vector3.one;
    private Vector3 stethoscopeBaseScale = Vector3.one;

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
        audioSource.Play();
        yield return new WaitForSeconds(Mathf.Max(0.1f, heartbeatDuration));
        audioSource.Stop();
        audioSource.loop = false;
    }

    private IEnumerator ShowStickerRoutine()
    {
        ResolveReferences();

        if (stickerObject == null)
            yield break;

        if (stickerAnchor != null)
            stickerObject.transform.SetPositionAndRotation(stickerAnchor.position, stickerAnchor.rotation);
        else
            PlaceFallbackStickerNearCamera();

        stickerObject.SetActive(true);
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

    private void ResolveReferences()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (popClip == null)
            popClip = Resources.Load<AudioClip>("pop") ?? FindAudioClipByName("pop");

        if (heartbeatClip == null)
            heartbeatClip = Resources.Load<AudioClip>("heartbeat-sound")
                ?? FindAudioClipByName("heartbeat-sound")
                ?? FindAudioClipByName("heartbeat");

        if (stickerObject == null)
            stickerObject = FindSceneObjectByName("Sticker");

        if (stickerObject == null)
            stickerObject = CreateSpriteSticker(FindSpriteByName("bear_sticker"));

        if (stickerObject == null)
            stickerObject = CreateStickerFromExistingBear();

        if (stickerObject == null)
            stickerObject = CreateFallbackSticker();

        // Scene sticker objects can carry an old oversized scale. Normalize the sticker
        // before it is shown so the reward stays the same size in every run.
        if (stickerObject != null && stickerObject.GetComponent<SpriteRenderer>() != null)
            stickerObject.transform.localScale = new Vector3(fallbackStickerSize.x, fallbackStickerSize.y, 1f);

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
        if (stickerObject == null || stickerAnchor != null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        Vector3 targetPosition = cameraTransform.position
            + cameraTransform.right * fallbackStickerCameraOffset.x
            + cameraTransform.up * fallbackStickerCameraOffset.y
            + cameraTransform.forward * fallbackStickerCameraOffset.z;

        stickerObject.transform.position = targetPosition;
        stickerObject.transform.rotation = Quaternion.LookRotation(stickerObject.transform.position - cameraTransform.position, cameraTransform.up);
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
