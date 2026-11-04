using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

// Part 7: choosing the blood-pressure cuff.
//   picture of the cuff sizes (the right one is highlighted and blinking)
//   -> the nurse tells Yaya she picked a cuff that fits her (keywords or Skip)
//   -> Yaya refuses and cries -> Mom explains why Yaya is afraid and asks to skip it
//   -> Quiz 7.
// Started automatically by Part6 after Quiz 6 is answered.
// All scene references are found by name at runtime; you can also assign them in the Inspector.
public class PediatricVitalSignsPart7Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        ChooseCuff
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [Tooltip("First name that exists is used.")]
    [SerializeField] private string promptPanelNames = "P7Instruction_Canvas|P6Instruction_Canvas|P5Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_7";

    [Header("Actors")]
    [SerializeField] private MomAnimationPlayer momAnimation;
    [SerializeField] private YayaAnimationPlayer yayaAnimation;
    [SerializeField] private Part3VisualDemoController part3VisualDemo;

    [Header("Prompt Text")]
    [SerializeField] private TMP_Text nursePromptText;

    [Header("Backend (keywords matched in the nurse's speech)")]
    [SerializeField] private RunAI_Network network;
    [SerializeField] private bool advanceFromBackendJson = true;
    [SerializeField] private bool ignoreFirstBackendJson = true;
    [SerializeField] private string chooseCuffKeywords = "帶子|壓脈帶|適合|幫你選|選這個|這個帶|這條|大小|尺寸|剛剛好|剛好";

    [Header("Cuff picture")]
    [Tooltip("Texture in a Resources folder.")]
    [SerializeField] private string cuffPictureResource = "Part7_CuffTypes";
    [Tooltip("Position in front of the eyes (x right, y up, z forward, meters).")]
    [SerializeField] private Vector3 cuffCardOffset = new Vector3(0f, -0.13f, 1.0f);
    [Tooltip("Width (m) of the whole row of cuffs.")]
    [SerializeField] private float cuffCardWidth = 0.86f;
    [Tooltip("How much a cuff grows while the cursor / controller ray is on it.")]
    [SerializeField] private float hoverScale = 1.22f;
    [Tooltip("Hide the medical-record button and the bear sticker while the cuffs are shown.")]
    [SerializeField] private bool hideHudWhileCuffsShown = true;
    [Tooltip("Seconds between the cuffs appearing and the nurse prompt (0 = together).")]
    [SerializeField] private float promptDelayAfterCuffs = 0f;
    [Header("Choosing the cuff")]
    [Tooltip("The nurse must also click the right cuff (not only say the keywords).")]
    [SerializeField] private bool requireCuffSelection = true;
    [Tooltip("0 = adult, 1 = child, 2 = infant, 3 = neonate.")]
    [SerializeField] private int correctCuffIndex = 1;
    [TextArea(1, 3)] [SerializeField] private string wrongCuffHint = "這條帶子不適合芽芽的手臂，請再選一次。";
    [TextArea(1, 3)] [SerializeField] private string clickCuffHint = "說得很好！請點選適合芽芽的壓脈帶。";
    [TextArea(1, 3)] [SerializeField] private string sayItHint = "選對了！請告訴芽芽你幫她選了這條帶子、大小很適合她。";
    [Tooltip("The cuff to point at, inside the picture: x, y of its centre and width, height (0-1, y from the top).")]
    [SerializeField] private Rect highlightArea = new Rect(0.381f, 0.367f, 0.205f, 0.335f);
    [Tooltip("White card and caption behind the picture. Off = only the cuffs are shown.")]
    [SerializeField] private bool showCardBackground = false;
    [Tooltip("Blinking yellow frame around the cuff that fits Yaya.")]
    [SerializeField] private bool showHighlightFrame = false;
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private float blinkPerSecond = 1.6f;

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip yayaRefuseClip;
    [SerializeField] private AudioClip momExplainClip;
    [SerializeField] private AudioClip momStruggleClip;
    [SerializeField] private AudioClip momAskSkipClip;
    [Header("Texts (edit here)")]
    [TextArea(2, 4)] [SerializeField] private string PromptChooseCuff =
        "請指著適合芽芽的壓脈帶，告訴芽芽你幫她選了這條帶子、大小很適合她。";
    [TextArea(1, 3)] [SerializeField] private string YayaRefuse = "我不要！痛痛！";
    [TextArea(2, 4)] [SerializeField] private string MomExplain = "那個 芽芽之前有量過血壓\n因為充氣的時候會緊緊的";
    [TextArea(2, 4)] [SerializeField] private string MomStruggle = "她就一直掙扎說會痛 之後她就蠻排斥量血壓的";
    [TextArea(1, 3)] [SerializeField] private string MomAskSkip = "請問她可以不要量血壓嗎？";

    [Header("Timing")]
    [Tooltip("Extra seconds after a line's audio clip has finished.")]
    [SerializeField] private float extraDelayAfterAudio = 0.4f;
    [Tooltip("Seconds per character when a line has no audio clip.")]
    [SerializeField] private float secondsPerCharacter = 0.16f;

    [Header("Next part")]
    [SerializeField] private bool startPart8AfterQuiz = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onQuizClosed;

    private const string CuffCaption = "各種尺寸的壓脈帶";

    private const string YayaName = "芽芽";
    private const string MomName = "媽媽";

    private Coroutine routine;
    private Coroutine quizCompletionRoutine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private bool skipRequested;
    private Button promptSkipButton;
    private int lastStartFrame = -1;

    private GameObject cuffCard;
    private readonly List<RectTransform> cuffItems = new List<RectTransform>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private XRRayInteractor[] rayInteractors;
    private float nextRayScan;
    private bool mouseUnavailable;
    private bool cuffChosen;
    private int wrongCuff = -1;
    private float wrongUntil;
    private bool triggerWasDown;
    private readonly List<UnityEngine.XR.InputDevice> xrDevices = new List<UnityEngine.XR.InputDevice>();

    // ---------------------------------------------------------------------------------
    // Entry
    // ---------------------------------------------------------------------------------
    public static PediatricVitalSignsPart7Flow FindOrCreate()
    {
        PediatricVitalSignsPart7Flow flow = FindObjectOfType<PediatricVitalSignsPart7Flow>(true);
        if (flow != null)
            return flow;

        GameObject host = null;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || !candidate.scene.IsValid())
                continue;
            string n = candidate.name.Replace("_", string.Empty).Replace(" ", string.Empty);
            if (string.Equals(n, "Part7Flow", StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }
        }

        if (host == null)
            host = new GameObject("Part7_Flow");

        return host.AddComponent<PediatricVitalSignsPart7Flow>();
    }

    public void StartPart7()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        enabled = true;

        if (lastStartFrame == Time.frameCount && routine != null)
            return;

        lastStartFrame = Time.frameCount;
        StopRoutine();
        ResetBackendGate();
        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
        Debug.Log("[Part7] StartPart7: choosing the blood-pressure cuff.", this);
        routine = StartCoroutine(Part7Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        skipRequested = true;
        waitingForNurseAction = WaitingForNurseAction.None;
    }

    public void SkipVoiceAndStartDialogue()
    {
        DebugSkipCurrentNurseCheck();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        HideCuffCard();
    }

    private void Update()
    {
        if (!advanceFromBackendJson || waitingForNurseAction == WaitingForNurseAction.None)
            return;

        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (network == null || string.IsNullOrWhiteSpace(network.LastJson))
            return;

        HandleBackendJson(network.LastJson);
    }

    private void LateUpdate()
    {
        if (cuffCard == null || !cuffCard.activeSelf)
            return;

        PlaceCuffCard();
        if (hideHudWhileCuffsShown)
            HideHud();
        UpdateCuffHover();
    }

    // ---------------------------------------------------------------------------------
    // Story
    // ---------------------------------------------------------------------------------
    private IEnumerator Part7Routine()
    {
        ResolveReferences();
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();

        // 0) The picture of the cuff sizes, on its own for a moment.
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        cuffChosen = false;
        wrongCuff = -1;
        ShowCuffCard();
        if (promptDelayAfterCuffs > 0f)
            yield return new WaitForSeconds(promptDelayAfterCuffs);

        // 1) The nurse tells Yaya she picked a cuff that fits her (keywords or Skip).
        //    She must say it (keywords) AND click the cuff that fits; Skip passes both.
        ShowNursePrompt(PromptChooseCuff, WaitingForNurseAction.ChooseCuff);
        skipRequested = false;
        yield return new WaitUntil(() => skipRequested
            || (waitingForNurseAction == WaitingForNurseAction.None && (!requireCuffSelection || cuffChosen)));
        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
        if (cuffChosen)
            yield return new WaitForSeconds(0.6f);
        HideCuffCard();

        // 2) Yaya refuses and cries.
        yayaAnimation?.PlaySittingDisbelief();
        momAnimation?.PlayStandingIdle();
        yield return ShowLine(YayaName, YayaRefuse, yayaRefuseClip);

        // 3) Mom explains to the nurse and asks whether it can be skipped.
        yield return ShowLine(MomName, MomExplain, momExplainClip);
        yield return ShowLine(MomName, MomStruggle, momStruggleClip);
        yield return ShowLine(MomName, MomAskSkip, momAskSkipClip);

        // 4) Quiz 7
        ShowQuiz();
        routine = null;
    }

    private IEnumerator ShowLine(string speaker, string content, AudioClip clip)
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(dialoguePanel, true, dialoguePanelChildName);

        if (dialogueManager != null)
        {
            dialogueManager.gameObject.SetActive(true);
            dialogueManager.StopPlayback();
            dialogueManager.ShowText(speaker, content);
        }
        else
        {
            Debug.LogWarning("[Part7] Dialogue manager is missing.", this);
        }

        float seconds = Mathf.Clamp(content.Length * secondsPerCharacter, 1.6f, 6f);
        if (clip != null)
        {
            if (dialogueAudioSource != null)
            {
                dialogueAudioSource.Stop();
                dialogueAudioSource.PlayOneShot(clip);
            }
            seconds = Mathf.Max(0.1f, AudioClipTrim.GetAudibleLength(clip) + Mathf.Max(0f, extraDelayAfterAudio));
        }
        yield return new WaitForSeconds(seconds);
    }

    // ---------------------------------------------------------------------------------
    // Cuff picture (built at runtime, shown in front of the eyes)
    // ---------------------------------------------------------------------------------
    private void ShowCuffCard()
    {
        if (cuffCard == null)
            BuildCuffCard();
        if (cuffCard == null)
            return;
        cuffCard.SetActive(true);
        PlaceCuffCard();
    }

    private void HideCuffCard()
    {
        if (cuffCard != null)
            cuffCard.SetActive(false);
        RestoreHud();
    }

    // The medical-record button and the bear sticker are not drawn while the cuffs are shown.
    private void HideHud()
    {
        foreach (MedicalRecordHudButton button in FindObjectsOfType<MedicalRecordHudButton>())
            HideAllUnder(button.transform);
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>();
        if (part3VisualDemo != null && part3VisualDemo.HudSticker != null)
            HideAllUnder(part3VisualDemo.HudSticker);
        // The emotion bar would sit behind the cuffs.
        if (emotionPresenter == null)
            emotionPresenter = FindObjectOfType<KidEmotionPresenter>();
        if (emotionPresenter != null && emotionPresenter.MeterRoot != null)
            HideAllUnder(emotionPresenter.MeterRoot);
    }
    private KidEmotionPresenter emotionPresenter;

    private void HideAllUnder(Transform root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.forceRenderingOff)
            {
                r.forceRenderingOff = true;
                hiddenRenderers.Add(r);
            }
        }
        foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
        {
            if (c.enabled)
            {
                c.enabled = false;
                hiddenCanvases.Add(c);
            }
        }
    }

    private void RestoreHud()
    {
        foreach (Renderer r in hiddenRenderers)
        {
            if (r != null)
                r.forceRenderingOff = false;
        }
        foreach (Canvas c in hiddenCanvases)
        {
            if (c != null)
                c.enabled = true;
        }
        hiddenRenderers.Clear();
        hiddenCanvases.Clear();
    }

    // The cuff under the mouse cursor / controller ray grows a little.
    private void UpdateCuffHover()
    {
        Transform card = cuffCard.transform;
        Plane plane = new Plane(card.forward, card.position);
        int hovered = -1;

        void Test(Ray ray)
        {
            if (hovered >= 0 || !plane.Raycast(ray, out float distance) || distance > 8f)
                return;
            Vector3 local = card.InverseTransformPoint(ray.GetPoint(distance));
            for (int i = 0; i < cuffItems.Count; i++)
            {
                RectTransform item = cuffItems[i];
                Vector2 half = item.sizeDelta * 0.5f;
                Vector2 centre = item.anchoredPosition;
                if (Mathf.Abs(local.x - centre.x) <= half.x && Mathf.Abs(local.y - centre.y) <= half.y)
                {
                    hovered = i;
                    return;
                }
            }
        }

        bool clicked = false;
        Camera cam = Camera.main;
#if ENABLE_INPUT_SYSTEM
        if (cam != null && UnityEngine.InputSystem.Mouse.current != null && (Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive))
        {
            Test(cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue()));
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                clicked = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (cam != null && !mouseUnavailable && (Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive))
        {
            try
            {
                Test(cam.ScreenPointToRay(Input.mousePosition));
                if (Input.GetMouseButtonDown(0))
                    clicked = true;
            }
            catch (InvalidOperationException)
            {
                mouseUnavailable = true;
            }
        }
#endif

        if (rayInteractors == null || Time.unscaledTime >= nextRayScan)
        {
            rayInteractors = FindObjectsOfType<XRRayInteractor>();
            nextRayScan = Time.unscaledTime + 2f;
        }
        foreach (XRRayInteractor interactor in rayInteractors)
        {
            if (interactor != null && interactor.isActiveAndEnabled)
                Test(new Ray(interactor.transform.position, interactor.transform.forward));
        }

        // VR: trigger pulled while a controller ray is on a cuff.
        bool triggerDown = false;
        xrDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand, xrDevices);
        foreach (UnityEngine.XR.InputDevice device in xrDevices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed)
                triggerDown = true;
        }
        if (triggerDown && !triggerWasDown)
            clicked = true;
        triggerWasDown = triggerDown;

        if (clicked && hovered >= 0 && !cuffChosen)
            OnCuffClicked(hovered);

        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        bool wrongShown = wrongCuff >= 0 && Time.unscaledTime < wrongUntil;
        int chosen = cuffChosen ? Mathf.Clamp(correctCuffIndex, 0, cuffItems.Count - 1) : -1;
        for (int i = 0; i < cuffItems.Count; i++)
        {
            bool big = i == chosen || (chosen < 0 && i == hovered);
            float target = big ? Mathf.Max(1f, hoverScale) : 1f;
            RectTransform item = cuffItems[i];
            item.localScale = Vector3.Lerp(item.localScale, Vector3.one * target, k);
            if (big)
                item.SetAsLastSibling();   // drawn on top of its neighbours

            // Chosen: the others fade. Wrong pick: it flashes red and shakes for a moment.
            Color tint = Color.white;
            float shake = 0f;
            if (chosen >= 0 && i != chosen)
                tint = new Color(1f, 1f, 1f, 0.3f);
            else if (wrongShown && i == wrongCuff)
            {
                tint = new Color(1f, 0.45f, 0.45f, 1f);
                shake = Mathf.Sin(Time.unscaledTime * 55f) * 6f * Mathf.Clamp01((wrongUntil - Time.unscaledTime) / 0.5f);
            }
            RawImage raw = item.GetComponent<RawImage>();
            if (raw != null)
                raw.color = Color.Lerp(raw.color, tint, k);
            item.localRotation = Quaternion.Euler(0f, 0f, shake);
        }
    }

    private void OnCuffClicked(int index)
    {
        if (index == Mathf.Clamp(correctCuffIndex, 0, cuffItems.Count - 1))
        {
            cuffChosen = true;
            wrongCuff = -1;
            PartScoreBoard.ReportAttempt("cuff", true);
            Debug.Log("[Part7] Right cuff chosen.", this);
            // Still has to say it: remind her.
            if (waitingForNurseAction != WaitingForNurseAction.None && nursePromptText != null && !string.IsNullOrEmpty(sayItHint))
                nursePromptText.text = sayItHint;
        }
        else
        {
            wrongCuff = index;
            wrongUntil = Time.unscaledTime + 0.7f;
            PartScoreBoard.ReportAttempt("cuff", false);
            Debug.Log($"[Part7] Wrong cuff ({index}).", this);
            if (nursePromptText != null && !string.IsNullOrEmpty(wrongCuffHint))
                nursePromptText.text = wrongCuffHint;
        }
    }

    private void PlaceCuffCard()
    {
        Camera cam = Camera.main;
        if (cuffCard == null || cam == null)
            return;

        Transform c = cam.transform;
        Transform t = cuffCard.transform;
        Vector3 cardOffset = HudDepth.Flatten(cuffCardOffset, out float depthFactor);
        t.position = c.position + c.right * cardOffset.x + c.up * cardOffset.y + c.forward * cardOffset.z;
        t.rotation = c.rotation;
        t.localScale = Vector3.one * (cuffCardWidth / 640f) * depthFactor;
    }

    // Each cuff of the picture (pixels: x, y from the top, width, height) is its own image,
    // so they can sit close together, be large, and grow one by one under the cursor.
    private static readonly Rect[] CuffPixels =
    {
        new Rect(30f, 100f, 385f, 520f),    // adult
        new Rect(452f, 132f, 333f, 451f),   // child
        new Rect(850f, 148f, 288f, 390f),   // infant
        new Rect(1213f, 162f, 252f, 364f),  // neonate
    };
    private const float CuffPictureWidth = 1600f;
    private const float CuffPictureHeight = 660f;

    private void BuildCuffCard()
    {
        Texture2D picture = Resources.Load<Texture2D>(cuffPictureResource);
        if (picture == null)
        {
            Debug.LogWarning($"[Part7] Picture 'Resources/{cuffPictureResource}' not found.", this);
            return;
        }

        const float cardWidth = 640f;
        const float gap = 14f;
        float pixels = 0f;
        float tallest = 0f;
        foreach (Rect r in CuffPixels)
        {
            pixels += r.width;
            tallest = Mathf.Max(tallest, r.height);
        }
        float unit = (cardWidth - gap * (CuffPixels.Length - 1)) / pixels;   // canvas units per picture pixel

        cuffCard = new GameObject("Part7_CuffPicture", typeof(RectTransform));
        Canvas canvas = cuffCard.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 45;
        cuffCard.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        RectTransform root = (RectTransform)cuffCard.transform;
        root.sizeDelta = new Vector2(cardWidth, tallest * unit);

        cuffItems.Clear();
        float x = -cardWidth * 0.5f;
        float top = tallest * unit * 0.5f;
        for (int i = 0; i < CuffPixels.Length; i++)
        {
            Rect r = CuffPixels[i];
            float w = r.width * unit;
            float h = r.height * unit;
            GameObject item = new GameObject("Cuff_" + i, typeof(RectTransform));
            item.transform.SetParent(root, false);
            RectTransform rect = (RectTransform)item.transform;
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * 0.5f, top - h * 0.5f);   // tops in one line
            RawImage raw = item.AddComponent<RawImage>();
            raw.texture = picture;
            raw.uvRect = new Rect(r.x / CuffPictureWidth, 1f - (r.y + r.height) / CuffPictureHeight,
                r.width / CuffPictureWidth, r.height / CuffPictureHeight);
            raw.raycastTarget = false;
            cuffItems.Add(rect);
            x += w + gap;
        }

        cuffCard.SetActive(false);
    }

    private static Image NewImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text NewText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, Color color, Vector2 position, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = box;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ---------------------------------------------------------------------------------
    // Prompt / quiz / backend helpers (same behaviour as Part5)
    // ---------------------------------------------------------------------------------
    private IEnumerator WaitForNurseActionOrSkip()
    {
        skipRequested = false;
        yield return new WaitUntil(() => waitingForNurseAction == WaitingForNurseAction.None || skipRequested);
        waitingForNurseAction = WaitingForNurseAction.None;
        skipRequested = false;
    }

    private void ShowNursePrompt(string prompt, WaitingForNurseAction waitingAction)
    {
        ResolveReferences();
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        SetPanelVisible(quizPanel, false, quizPanelChildName);
        SetPanelVisible(nursePromptPanel, true, "TopHint_Panel");
        SetPromptSkipButtonsVisible(waitingAction != WaitingForNurseAction.None);

        if (nursePromptText != null)
            nursePromptText.text = prompt;
        else
            Debug.LogWarning("[Part7] Nurse prompt text not found.", this);

        waitingForNurseAction = waitingAction;
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        part3VisualDemo?.TemporarilyHideStickerForQuiz();
        SetPanelVisible(quizPanel, true, quizPanelChildName);

        QuizPanelRuntimeHelper.BeginQuiz(quizPanel, quizPanelChildName);
        if (quizCompletionRoutine != null)
            StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosed());
    }

    private IEnumerator WaitForQuizPanelClosed()
    {
        GameObject watched = null;
        if (quizPanel != null)
        {
            Transform child = FindDeepChild(quizPanel.transform, quizPanelChildName);
            watched = child != null ? child.gameObject : quizPanel;
        }

        yield return null;
        while (watched != null && watched.activeInHierarchy)
            yield return null;

        quizCompletionRoutine = null;
        QuizPanelRuntimeHelper.EndQuiz();
        part3VisualDemo?.RestoreStickerAfterQuiz();
        Debug.Log("[Part7] Quiz 7 closed.", this);
        onQuizClosed?.Invoke();

        if (startPart8AfterQuiz)
            PediatricVitalSignsPart8Flow.FindOrCreate().StartPart8();
    }

    private void HandleBackendJson(string json)
    {
        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords = waitingForNurseAction == WaitingForNurseAction.ChooseCuff ? chooseCuffKeywords : string.Empty;

        if (ContainsAnyKeyword(speechText, keywords))
        {
            // Yaya is in meltdown (red): the nurse must calm her before the story moves on.
            if (KidEmotionGate.Blocking)
            {
                Debug.Log("[Part7] Keywords matched, but Yaya must be calmed first.", this);
                return;
            }

            Debug.Log($"[Part7] Backend matched {waitingForNurseAction}. text={speechText}", this);
            // Said it, but the cuff still has to be clicked.
            if (waitingForNurseAction == WaitingForNurseAction.ChooseCuff && requireCuffSelection && !cuffChosen
                && nursePromptText != null && !string.IsNullOrEmpty(clickCuffHint))
                nursePromptText.text = clickCuffHint;
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part7] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
    }

    private void ResetBackendGate()
    {
        lastProcessedBackendJson = null;
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (network != null && ignoreFirstBackendJson && !string.IsNullOrWhiteSpace(network.LastJson))
            lastProcessedBackendJson = network.LastJson;
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        SetPromptSkipButtonsVisible(false);
        HideCuffCard();
    }

    private void ResolveReferences()
    {
        if (nursePromptPanel == null)
        {
            foreach (string raw in promptPanelNames.Split('|'))
            {
                nursePromptPanel = FindSceneObjectByName(raw.Trim());
                if (nursePromptPanel != null)
                    break;
            }
        }

        if (dialoguePanel == null)
            dialoguePanel = FindSceneObjectByName("Newnewnew Dialogue_Canvas") ?? FindSceneObjectByName("Dialogue_Canvas");
        if (quizPanel == null)
            quizPanel = FindSceneObjectByName("NewQuizCanvas") ?? FindSceneObjectByName("QuizCanvas");
        if (momAnimation == null)
            momAnimation = FindObjectOfType<MomAnimationPlayer>(true);
        if (yayaAnimation == null)
            yayaAnimation = FindObjectOfType<YayaAnimationPlayer>(true);
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);
        if (network == null)
            network = FindObjectOfType<RunAI_Network>();
        if (dialogueManager == null && dialoguePanel != null)
            dialogueManager = dialoguePanel.GetComponentInChildren<NewDialogueManager>(true);
        if (dialogueManager == null)
            dialogueManager = FindObjectOfType<NewDialogueManager>(true);
        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = GetComponent<AudioSource>();
            if (dialogueAudioSource == null)
                dialogueAudioSource = gameObject.AddComponent<AudioSource>();
            dialogueAudioSource.playOnAwake = false;
            dialogueAudioSource.spatialBlend = 0f;
        }

        ResolvePromptText();
    }

    private void ResolvePromptText()
    {
        if (nursePromptPanel == null)
            return;
        if (nursePromptText != null && nursePromptText.transform.IsChildOf(nursePromptPanel.transform))
            return;

        foreach (string raw in promptTextChildNames.Split('|'))
        {
            Transform child = FindDeepChild(nursePromptPanel.transform, raw.Trim());
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                nursePromptText = text;
                return;
            }
        }

        foreach (TMP_Text text in nursePromptPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.name.IndexOf("Skip", StringComparison.OrdinalIgnoreCase) < 0)
            {
                nursePromptText = text;
                return;
            }
        }
    }

    private void SetPromptSkipButtonsVisible(bool visible)
    {
        EnsurePromptSkipButton();
        if (promptSkipButton != null)
            promptSkipButton.gameObject.SetActive(visible);
    }

    private void EnsurePromptSkipButton()
    {
        if (nursePromptPanel == null)
            return;

        if (promptSkipButton == null || !promptSkipButton.transform.IsChildOf(nursePromptPanel.transform))
        {
            Transform existing = FindDeepChild(nursePromptPanel.transform, "SkipVoice_Button");
            if (existing != null)
            {
                promptSkipButton = existing.GetComponent<Button>();
                if (promptSkipButton == null)
                    promptSkipButton = existing.gameObject.AddComponent<Button>();
            }
        }

        if (promptSkipButton == null)
            return;

        // Part5 uses the same panel/button: make sure the click goes to the flow that is running.
        promptSkipButton.onClick.RemoveListener(DebugSkipCurrentNurseCheck);
        promptSkipButton.onClick.AddListener(DebugSkipCurrentNurseCheck);
    }

    private static bool ContainsAnyKeyword(string text, string keywords)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keywords))
            return false;
        foreach (string part in keywords.Split('|'))
        {
            string keyword = part.Trim();
            if (keyword.Length > 0 && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string ExtractBackendSpeechText(string json)
    {
        try
        {
            BackendSpeechResponse response = JsonUtility.FromJson<BackendSpeechResponse>(json);
            if (response != null)
                return ((response.llm_window_text ?? "") + " " + (response.text ?? "") + " " + (response.raw_text ?? "")).Trim();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Part7] Failed to parse backend json: " + e.Message);
        }
        return string.Empty;
    }

    private static void SetPanelVisible(GameObject target, bool visible, string preferredChildName)
    {
        if (target == null)
            return;
        if (visible && !target.activeSelf)
            target.SetActive(true);

        if (target.GetComponent<Canvas>() != null)
        {
            Transform child = FindDeepChild(target.transform, preferredChildName);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
                return;
            }
        }
        target.SetActive(visible);
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;
        GameObject active = GameObject.Find(objectName);
        if (active != null)
            return active;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate != null && candidate.name == objectName && candidate.scene.IsValid())
                return candidate;
        }
        return null;
    }

    [Serializable]
    private class BackendSpeechResponse
    {
        public string text;
        public string raw_text;
        public string llm_window_text;
    }
}
