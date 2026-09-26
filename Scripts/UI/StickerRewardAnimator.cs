using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StickerRewardAnimator : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image stickerImage;
    [SerializeField] private Sprite stickerSprite;
    [SerializeField] private Vector2 startAnchoredPosition = new Vector2(0f, 80f);
    [SerializeField] private Vector2 stickerSize = new Vector2(160f, 160f);

    [Header("Target")]
    [SerializeField] private Transform targetWorldAnchor;
    [SerializeField] private RectTransform targetUiAnchor;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector2 targetOffset;

    [Header("Timing")]
    [SerializeField] private float popDuration = 0.25f;
    [SerializeField] private float holdDuration = 0.8f;
    [SerializeField] private float flyDuration = 0.75f;
    [SerializeField] private float targetScale = 0.2f;
    [SerializeField] private float settlePulseDuration = 0.2f;
    [SerializeField] private bool hideAfterArrive = false;

    private Coroutine routine;
    private RectTransform stickerRect;
    private CanvasGroup stickerCanvasGroup;

    private void Awake()
    {
        ResolveReferences();
        HideImmediate();
    }

    public void Play()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveReferences();

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine());
    }

    public IEnumerator PlayAndWait()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveReferences();

        if (routine != null)
            StopCoroutine(routine);

        yield return PlayRoutine();
    }

    public void HideImmediate()
    {
        ResolveReferences();

        if (stickerImage != null)
            stickerImage.enabled = false;

        if (stickerCanvasGroup != null)
            stickerCanvasGroup.alpha = 0f;
    }

    private IEnumerator PlayRoutine()
    {
        if (stickerRect == null || stickerImage == null || canvas == null)
        {
            Debug.LogWarning("[StickerRewardAnimator] Missing sticker Image or Canvas.", this);
            routine = null;
            yield break;
        }

        if (stickerSprite != null)
            stickerImage.sprite = stickerSprite;

        stickerImage.enabled = true;
        stickerRect.anchoredPosition = startAnchoredPosition;
        stickerRect.sizeDelta = stickerSize;
        stickerRect.localScale = Vector3.zero;

        if (stickerCanvasGroup != null)
            stickerCanvasGroup.alpha = 1f;

        yield return ScaleRoutine(Vector3.zero, Vector3.one, popDuration);
        yield return new WaitForSeconds(Mathf.Max(0f, holdDuration));

        Vector2 start = stickerRect.anchoredPosition;
        Vector2 target = GetTargetAnchoredPosition();
        Vector3 targetLocalScale = Vector3.one * Mathf.Max(0.01f, targetScale);
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, flyDuration)));
            stickerRect.anchoredPosition = Vector2.Lerp(start, target, t);
            stickerRect.localScale = Vector3.Lerp(Vector3.one, targetLocalScale, t);
            yield return null;
        }

        stickerRect.anchoredPosition = target;
        yield return ScaleRoutine(targetLocalScale, targetLocalScale * 1.18f, settlePulseDuration * 0.5f);
        yield return ScaleRoutine(targetLocalScale * 1.18f, targetLocalScale, settlePulseDuration * 0.5f);

        if (hideAfterArrive)
            HideImmediate();

        routine = null;
    }

    private IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            stickerRect.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            stickerRect.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        stickerRect.localScale = to;
    }

    private Vector2 GetTargetAnchoredPosition()
    {
        if (targetUiAnchor != null)
            return WorldOrScreenToCanvasPosition(targetUiAnchor.position) + targetOffset;

        if (targetWorldAnchor != null)
            return WorldOrScreenToCanvasPosition(GetWorldCamera().WorldToScreenPoint(targetWorldAnchor.position)) + targetOffset;

        return startAnchoredPosition + targetOffset;
    }

    private Vector2 WorldOrScreenToCanvasPosition(Vector3 screenPosition)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return startAnchoredPosition;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint);
        return localPoint;
    }

    private Camera GetWorldCamera()
    {
        if (worldCamera != null)
            return worldCamera;

        if (Camera.main != null)
            return Camera.main;

        return canvas != null ? canvas.worldCamera : null;
    }

    private void ResolveReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (stickerImage == null)
            stickerImage = GetComponentInChildren<Image>(true);

        if (stickerImage == null && canvas != null)
        {
            GameObject imageObject = new GameObject("Sticker_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            imageObject.transform.SetParent(transform, false);
            stickerImage = imageObject.GetComponent<Image>();
        }

        if (stickerImage == null)
            return;

        stickerRect = stickerImage.rectTransform;
        stickerCanvasGroup = stickerImage.GetComponent<CanvasGroup>();
        if (stickerCanvasGroup == null)
            stickerCanvasGroup = stickerImage.gameObject.AddComponent<CanvasGroup>();

        stickerImage.raycastTarget = false;
        stickerImage.preserveAspect = true;
    }

    private static float EaseInOut(float t)
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
