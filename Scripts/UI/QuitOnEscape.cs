using TMPro;
using UnityEngine;
using UnityEngine.UI;

// PC version (no headset): Esc asks "確定要離開嗎？". Esc again, or the 離開 button, closes the
// program; any click on 繼續 (or waiting a few seconds) goes back to the game.
// Created on its own at start. Does nothing with a headset on (Quest), where there is no Esc key.
public class QuitOnEscape : MonoBehaviour
{
    [Tooltip("Seconds the question stays up before it closes itself.")]
    public float autoHideSeconds = 6f;

    GameObject panel;
    float shownAt = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindObjectOfType<QuitOnEscape>() != null)
            return;
        if (!Application.isEditor && UnityEngine.XR.XRSettings.isDeviceActive)
            return;
        GameObject go = new GameObject("QuitOnEscape");
        DontDestroyOnLoad(go);
        go.AddComponent<QuitOnEscape>();
    }

    void Update()
    {
        if (EscPressed())
        {
            if (panel != null && panel.activeSelf)
                Quit();
            else
                Show();
        }

        if (panel != null && panel.activeSelf && Time.unscaledTime - shownAt > Mathf.Max(1f, autoHideSeconds))
            Hide();
    }

    static bool EscPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
            return keyboard.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    void Show()
    {
        if (panel == null)
            Build();
        panel.SetActive(true);
        shownAt = Time.unscaledTime;
    }

    void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void Quit()
    {
        Debug.Log("[QuitOnEscape] Quit");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Build()
    {
        TMP_FontAsset font = FindChineseFont();

        panel = new GameObject("QuitOnEscape_Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        Canvas canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // above everything else
        CanvasScaler scaler = panel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        panel.AddComponent<GraphicRaycaster>();

        // Dim the game behind the question.
        Image dim = NewImage(panel.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);

        Image box = NewImage(panel.transform, "Box", new Color(0.08f, 0.13f, 0.21f, 0.97f));
        box.rectTransform.sizeDelta = new Vector2(720f, 340f);

        NewText(box.transform, font, "確定要離開嗎？", 52f, new Vector2(0f, 80f), new Vector2(660f, 80f));
        NewText(box.transform, font, "再按一次 Esc 也可以離開", 28f, new Vector2(0f, 15f), new Vector2(660f, 50f))
            .color = new Color(0.8f, 0.86f, 0.94f, 1f);

        NewButton(box.transform, font, "繼續", new Vector2(-150f, -90f), new Color(0.24f, 0.55f, 0.85f, 1f), Hide);
        NewButton(box.transform, font, "離開", new Vector2(150f, -90f), new Color(0.82f, 0.30f, 0.32f, 1f), Quit);

        panel.SetActive(false);
    }

    static TMP_FontAsset FindChineseFont()
    {
        // The story's instruction canvases use the font that has the Chinese characters.
        foreach (Canvas other in FindObjectsOfType<Canvas>(true))
        {
            if (other == null || !other.name.Contains("Instruction_Canvas"))
                continue;
            TMP_Text sample = other.GetComponentInChildren<TMP_Text>(true);
            if (sample != null && sample.font != null)
                return sample.font;
        }
        foreach (TMP_Text text in FindObjectsOfType<TMP_Text>(true))
            if (text != null && text.font != null && text.font.name.ToUpperInvariant().Contains("MSJH"))
                return text.font;
        return null;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI NewText(Transform parent, TMP_FontAsset font, string value, float size, Vector2 position, Vector2 box)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = value;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void NewButton(Transform parent, TMP_FontAsset font, string label, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
    {
        Image image = NewImage(parent, "Button_" + label, color);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = new Vector2(240f, 84f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        NewText(image.transform, font, label, 36f, Vector2.zero, new Vector2(240f, 84f));
    }
}
