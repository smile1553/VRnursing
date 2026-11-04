using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

// Part 9 - Therapeutic play before the blood pressure:
//   the nurse shows on the teddy bear what the cuff feels like (tight, not painful), squeezes
//   Yaya's arm gently, reassures, praises the bear and gives it a sticker  ->  Yaya agrees
//   ->  picture of the cuff and the artery  ->  the nurse puts the cuff on Yaya  ->  the
//   blood pressure is measured.
//
// Started automatically by Part8 after Quiz 8 is answered.
// All scene references are found by name at runtime; you can also assign them in the Inspector.
public class PediatricVitalSignsPart9Flow : MonoBehaviour
{
    private enum WaitingForNurseAction
    {
        None,
        BearDemo,
        PraiseSticker,
        WrapCuff
    }

    [Header("Panels")]
    [SerializeField] private GameObject nursePromptPanel;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject quizPanel;
    [Tooltip("First name that exists is used.")]
    [SerializeField] private string promptPanelNames = "P9Instruction_Canvas|P8Instruction_Canvas|P7Instruction_Canvas|P6Instruction_Canvas|P5Instruction_Canvas";
    [SerializeField] private string promptTextChildNames = "Prompt_Text|Instruction_Text|NursePromptText|Text (TMP)|Text";
    [SerializeField] private string dialoguePanelChildName = "Dialogue_Panel";
    [SerializeField] private string quizPanelChildName = "Quiz_Panel_9";

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
    [Tooltip("Each step passes when the nurse's sentence contains any of its keywords (separated by |).")]
    [SerializeField] private string bearDemoKeywords = "好緊|緊緊|捏|不是痛|不會痛|不痛|愈來愈緊|越來越緊|怕怕|不要亂動|不要動|一下子|一下就好";
    [SerializeField] private string praiseStickerKeywords = "好棒|勇敢|貼紙";

    [Header("Dialogue")]
    [SerializeField] private NewDialogueManager dialogueManager;
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip yayaAgreeClip;
    [Tooltip("Extra seconds after a line's audio clip has finished.")]
    [SerializeField] private float extraDelayAfterAudio = 0.4f;
    [Tooltip("Seconds per character when a line has no audio clip.")]
    [SerializeField] private float secondsPerCharacter = 0.16f;

    [Header("Texts (edit here)")]
    [TextArea(2, 4)] [SerializeField] private string promptBearDemo = "請用小熊示範，跟芽芽說明壓脈帶充氣時會有什麼感覺。";
    [TextArea(2, 4)] [SerializeField] private string promptPraiseSticker = "請稱讚小熊並送牠一張貼紙，再邀請芽芽也試試看。";
    [TextArea(1, 3)] [SerializeField] private string yayaAgree = "好！要輕輕地綁喔！";
    [TextArea(2, 4)] [SerializeField] private string promptWrapCuff = "要幫芽芽綁壓脈帶了。請在圖上點選：箭頭要對準哪裡？";
    [TextArea(1, 3)] [SerializeField] private string wrongChoiceText = "不是這裡喔，再想想動脈在哪裡。";
    [TextArea(1, 3)] [SerializeField] private string answerText = "答對了！箭頭要對準上臂內側的肱動脈。";
    [SerializeField] private float answerSeconds = 4f;
    [TextArea(1, 3)] [SerializeField] private string finishedText = "血壓測量完成！芽芽好勇敢！";

    [Header("Cuff and artery picture")]
    [Tooltip("Texture in a Resources folder.")]
    [SerializeField] private string questionPictureResource = "Part9_CuffQuestion";
    [SerializeField] private string answerPictureResource = "Part9_CuffArtery";
    [Tooltip("Position in front of the eyes (x right, y up, z forward, meters). Just below the prompt.")]
    [SerializeField] private Vector3 armPictureCentre = new Vector3(0f, -0.05f, 1.0f);
    [SerializeField] private float armPictureWide = 0.70f;
    [Tooltip("The three choices on the question picture: x from the left, y from the top (0-1).")]
    [SerializeField] private Vector2[] armChoiceSpots = { new Vector2(0.3071f, 0.4096f), new Vector2(0.4179f, 0.56f), new Vector2(0.7714f, 0.5248f) };
    [Tooltip("0 = A, 1 = B, 2 = C.")]
    [SerializeField] private int correctChoice = 1;
    [Tooltip("How close to a choice a click must be (part of the picture's width).")]
    [SerializeField] private float choiceRadius = 0.06f;

    [Header("Blood pressure measurement")]
    [Tooltip("Where the toy cuff sits on the bear. Across: sideways from its middle (1 = its edge; negative = the arm the nurse sees on her LEFT, away from Yaya's hand). Up: part of its height above its middle. Front: towards the nurse (part of its half depth).")]
    [SerializeField] private float bearCuffAcross = -0.74f;
    [SerializeField] private float bearCuffUp = 0.08f;
    [SerializeField] private float bearCuffFront = 0.3f;
    [Tooltip("Size of the toy cuff: width across the arm and length along the arm, as parts of the bear's height.")]
    [SerializeField] private Vector2 bearCuffSize = new Vector2(0.34f, 0.2f);
    [SerializeField] private Color bearCuffColor = new Color(0.18f, 0.45f, 0.98f);
    [Tooltip("Find the bear's arm from its mesh (the part sticking out sideways at chest height) and put the cuff on it. Off = use Across / Up / Front above.")]
    [SerializeField] private bool bearCuffFindArm = true;
    [Tooltip("Where along the arm (0 = where it leaves the body, 1 = the paw).")]
    [Range(0f, 1f)] [SerializeField] private float bearCuffAlongArm = 0.45f;
    [Tooltip("Fine shift of the bear cuff (m): x = toward the arm's side, y = up, z = toward the nurse. Can be changed while playing.")]
    [SerializeField] private Vector3 bearCuffNudge = Vector3.zero;
    [Tooltip("Extra turn of the bear cuff around the line toward the nurse (degrees). Can be changed while playing.")]
    [SerializeField] private float bearCuffTurn = 0f;
    [SerializeField] private Color cuffColor = new Color(0.16f, 0.32f, 0.63f);

    [Header("Blood pressure machine (on its own stand) beside Yaya")]
    [Tooltip("Show the blood pressure machine beside Yaya during this part, with a tube to the cuff.")]
    [SerializeField] private bool showMachineTrolley = true;
    [Tooltip("Machine prefab in Assets/Resources (copy of the Blue Dot Heart_Rate_Monitor on its stand). Missing = a simple box.")]
    [SerializeField] private string machineResource = "BP_Monitor";
    [Tooltip("Also put a trolley under the machine (off = the machine stands on the floor by itself).")]
    [SerializeField] private bool useTrolley = false;
    [SerializeField] private string trolleyResource = "BP_Trolley";
    [Tooltip("Reading screen on the machine: x right, y down from the top of the machine, z toward the nurse from its middle (m). Can be changed while playing.")]
    [SerializeField] private Vector3 machineScreenOffset = new Vector3(0f, 0.13f, 0.1f);
    [SerializeField] private float machineScreenWidth = 0.2f;
    [Tooltip("Where the trolley stands, from Yaya's middle: sideways toward the arm with the cuff, and toward the nurse (m). Only used when the bed is not found.")]
    [SerializeField] private float trolleySideways = 0.6f;
    [SerializeField] private float trolleyTowardNurse = 0.3f;
    [Tooltip("Put the machine in the room when the scene starts (fixed place), instead of only in this part.")]
    [SerializeField] private bool machineFromStart = true;
    [Tooltip("To put the machine exactly where you want: add an empty object with this name to the scene (blue arrow = screen direction).")]
    [SerializeField] private string machineSpotName = "BP_Machine_Spot";
    [Tooltip("Turn of the machine model itself (degrees): its screen is on the side opposite to its forward axis, so 180 makes the screen face the nurse. Try 90 / -90 if it faces sideways.")]
    [SerializeField] private float machineModelYaw = 180f;
    [Tooltip("Without that object: how far beside Yaya, on the side away from Mom (m).")]
    [SerializeField] private float machineBesideYaya = 0.75f;
    [Tooltip("...and how far toward the room from Yaya's hips (m; negative = toward the wall).")]
    [SerializeField] private float machineTowardRoom = 0.25f;
    [Tooltip("Extra turn of the trolley + monitor (degrees) if the monitor's screen does not face the nurse.")]
    [SerializeField] private float trolleyYawOffset = 0f;
    [Tooltip("Size of the simple box machine, only used when the prefab is missing (m).")]
    [SerializeField] private float monitorWidth = 0.32f;
    [SerializeField] private Color tubeColor = new Color(0.12f, 0.12f, 0.14f);
    [Tooltip("Pick a new reading each run, inside the ranges below.")]
    [SerializeField] private bool randomReading = true;
    [Tooltip("Systolic range (mmHg), used when Random Reading is on.")]
    [SerializeField] private Vector2Int systolicRange = new Vector2Int(88, 104);
    [Tooltip("Diastolic range (mmHg), used when Random Reading is on.")]
    [SerializeField] private Vector2Int diastolicRange = new Vector2Int(50, 64);
    [SerializeField] private int systolic = 96;
    [SerializeField] private int diastolic = 58;
    [SerializeField] private float inflateSeconds = 2.5f;
    [SerializeField] private float deflateSeconds = 3f;
    [SerializeField] private Vector3 readingCardOffset = new Vector3(0.24f, -0.06f, 0.95f);
    [SerializeField] private float readingCardWidth = 0.30f;

    [Header("Quiz size")]
    [Tooltip("Size of Quiz 8 compared with how it was laid out (1 = unchanged, smaller = smaller).")]
    [SerializeField] private float quizPanelSize = 0.9f;

    [Header("Events")]
    [SerializeField] private UnityEvent onQuizClosed;

    private Coroutine routine;
    private Coroutine quizCompletionRoutine;
    private WaitingForNurseAction waitingForNurseAction = WaitingForNurseAction.None;
    private string lastProcessedBackendJson;
    private bool skipRequested;
    private Button promptSkipButton;
    private int lastStartFrame = -1;
    private bool stickerStepSkipped;

    private GameObject pictureCard;
    private RawImage pictureImage;
    private RectTransform choiceRing;
    private bool pictureIsAnswer;
    private bool pictureAnswered;
    private int wrongChoice = -1;
    private float wrongUntil;
    private bool triggerWasDown;
    private XRRayInteractor[] rayInteractors;
    private float nextRayScan;
    private readonly List<UnityEngine.XR.InputDevice> xrDevices = new List<UnityEngine.XR.InputDevice>();
    private GameObject readingCard;
    private TMP_Text readingValueText;
    private TMP_Text readingStatusText;
    private Transform bearCuff;
    private Transform bearCuffBear;
    private Vector3 bearCuffBaseLocal, bearCuffArmLocal, bearCuffSideLocal, bearCuffTowardLocal;
    private Transform yayaCuff;
    private Transform cuffArm, cuffForeArm;
    private float cuffInflate;
    private GameObject machine;
    private Transform machineTubeStart;
    private LineRenderer machineTube;
    private TMP_Text machineValueText;
    private Transform machineScreen;
    private float machineTopLocal;
    private Vector3 machineToward;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    // ---------------------------------------------------------------------------------
    // Entry
    // ---------------------------------------------------------------------------------
    public static PediatricVitalSignsPart9Flow FindOrCreate()
    {
        PediatricVitalSignsPart9Flow flow = FindObjectOfType<PediatricVitalSignsPart9Flow>(true);
        if (flow != null)
            return flow;

        GameObject host = null;
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate == null || !candidate.scene.IsValid())
                continue;
            string n = candidate.name.Replace("_", string.Empty).Replace(" ", string.Empty);
            if (string.Equals(n, "Part9Flow", StringComparison.OrdinalIgnoreCase))
            {
                host = candidate;
                break;
            }
        }

        if (host == null)
            host = new GameObject("Part9_Flow");

        return host.AddComponent<PediatricVitalSignsPart9Flow>();
    }

    public void StartPart9()
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
        Debug.Log("[Part9] StartPart9: bear demonstration, then the blood pressure.", this);
        routine = StartCoroutine(Part9Routine());
    }

    public void DebugSkipCurrentNurseCheck()
    {
        skipRequested = true;
        stickerStepSkipped = true;
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

    // ---------------------------------------------------------------------------------
    // Story
    // ---------------------------------------------------------------------------------
    private IEnumerator Part9Routine()
    {
        ResolveReferences();
        KidEmotionPresenter.Hold(false);
        yayaAnimation?.PlaySittingIdle();
        momAnimation?.PlayStandingIdle();
        HidePicture();
        HideReadingCard();
        EnsureMachine();   // it already stands in its place since the start; only the cuff is new

        // 1) On the bear: what the cuff feels like, squeezing Yaya's arm gently, reassuring
        //    (one step: any of those sentences passes, or Skip).
        ShowBearCuff();
        ShowNursePrompt(promptBearDemo, WaitingForNurseAction.BearDemo);
        yield return WaitForNurseActionOrSkip();

        // 2) Praise the bear + a sticker (said AND given, like the other sticker steps).
        ShowNursePrompt(promptPraiseSticker, WaitingForNurseAction.PraiseSticker);
        yield return WaitForStickerAndWords();
        HideBearCuff();

        // 3) Yaya agrees.
        yayaAnimation?.PlaySittingIdle();
        yield return ShowLine(YayaName, yayaAgree, yayaAgreeClip);

        // 4) Picture question: where does the cuff's arrow go? The nurse clicks the spot
        //    (no speech needed); the answer picture is shown, then the cuff goes on Yaya.
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        ShowNursePrompt(promptWrapCuff, WaitingForNurseAction.WrapCuff);
        pictureAnswered = false;
        wrongChoice = -1;
        ShowPicture(false);
        skipRequested = false;
        yield return new WaitUntil(() => pictureAnswered || skipRequested);
        skipRequested = false;
        waitingForNurseAction = WaitingForNurseAction.None;
        SetPromptSkipButtonsVisible(false);
        ShowPicture(true);
        if (nursePromptText != null && !string.IsNullOrEmpty(answerText))
            nursePromptText.text = answerText;
        yield return new WaitForSeconds(Mathf.Max(0.5f, answerSeconds));
        HidePicture();

        // 5) The blood pressure is measured.
        SetPromptSkipButtonsVisible(false);
        yield return MeasureBloodPressure();

        if (nursePromptText != null && !string.IsNullOrEmpty(finishedText))
            nursePromptText.text = finishedText;
        KidEmotionPresenter.Calm(15f, string.Empty);
        yield return new WaitForSeconds(3f);
        HideYayaCuff();
        ResetMachineScreen();   // the machine stays where it stands
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        Debug.Log("[Part9] Blood pressure measured: part finished.", this);
        onQuizClosed?.Invoke();
        // The story is over: work out the score and send the result to the backend.
        PartScoreBoard.Complete();
        routine = null;
    }

    private const string YayaName = "芽芽";

    // Sticker step: the nurse must say it AND give the sticker (any order); Skip passes both.
    private IEnumerator WaitForStickerAndWords()
    {
        if (part3VisualDemo == null)
            part3VisualDemo = FindObjectOfType<Part3VisualDemoController>(true);
        if (part3VisualDemo == null)
        {
            yield return WaitForNurseActionOrSkip();
            yield break;
        }

        part3VisualDemo.ResetHudStickerSelection();
        part3VisualDemo.GiveNextStickerToBear();   // this one is for the bear
        part3VisualDemo.ShowSticker();
        part3VisualDemo.BeginStickerGive();
        stickerStepSkipped = false;
        skipRequested = false;
        yield return new WaitUntil(() => stickerStepSkipped || skipRequested
            || (waitingForNurseAction == WaitingForNurseAction.None && part3VisualDemo.HudStickerWasSelected));
        stickerStepSkipped = false;
        skipRequested = false;
        waitingForNurseAction = WaitingForNurseAction.None;
        part3VisualDemo.EndStickerGive();
        yield return new WaitForSeconds(0.8f);
    }

    private void LateUpdate()
    {
        if (pictureCard != null && pictureCard.activeSelf)
        {
            PlaceInFrontOfEyes(pictureCard.transform, armPictureCentre, armPictureWide / 700f);
            HideHud();
            UpdatePictureClick();
        }
        if (readingCard != null && readingCard.activeSelf)
        {
            PlaceInFrontOfEyes(readingCard.transform, readingCardOffset, readingCardWidth / 300f);
            // The emotion bar sits in the same place: not shown while the reading is on screen.
            if (emotionPresenter == null)
                emotionPresenter = FindObjectOfType<KidEmotionPresenter>();
            if (emotionPresenter != null && emotionPresenter.MeterRoot != null)
                HideAllUnder(emotionPresenter.MeterRoot);
        }

        PlaceBearCuff();
        PlaceMachineScreen();
        UpdateMachineTube();

        // The cuff follows Yaya's upper arm (and swells while it fills).
        if (yayaCuff != null && cuffArm != null && cuffForeArm != null)
        {
            Vector3 a = cuffArm.position;
            Vector3 b = cuffForeArm.position;
            float length = Vector3.Distance(a, b);
            if (length > 1e-4f)
            {
                float diameter = length * (0.50f + 0.06f * cuffInflate);
                yayaCuff.SetPositionAndRotation(Vector3.Lerp(a, b, 0.55f), Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
                yayaCuff.localScale = new Vector3(diameter, length * 0.27f, diameter);
            }
        }
    }

    private void OnDisable()
    {
        HidePicture();
        HideReadingCard();
        HideBearCuff();
        HideYayaCuff();
        // The machine is part of the room now: it stays.
    }

    private static void PlaceInFrontOfEyes(Transform t, Vector3 offset, float scale)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Transform c = cam.transform;
        offset = HudDepth.Flatten(offset, out float depthFactor);
        t.position = c.position + c.right * offset.x + c.up * offset.y + c.forward * offset.z;
        t.rotation = c.rotation;
        t.localScale = Vector3.one * scale * depthFactor;
    }

    // ---------------------------------------------------------------------------------
    // Cuffs (simple blue bands)
    // ---------------------------------------------------------------------------------
    private Transform NewCuff(string name)
    {
        GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cuff.name = name;
        Collider collider = cuff.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        Renderer renderer = cuff.GetComponent<Renderer>();
        renderer.material.color = cuffColor;
        if (renderer.material.HasProperty("_Glossiness"))
            renderer.material.SetFloat("_Glossiness", 0.1f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return cuff.transform;
    }

    private void ShowBearCuff()
    {
        HideBearCuff();
        Transform bear = yayaAnimation != null ? yayaAnimation.FindComfortBear() : null;
        Camera cam = Camera.main;
        if (bear == null || cam == null)
            return;

        // The bear itself = its biggest mesh (stickers or other things attached to it are ignored).
        Renderer body = null;
        float biggest = -1f;
        foreach (Renderer r in bear.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is SpriteRenderer)
                continue;
            Vector3 e = r.bounds.size;
            float volume = e.x * e.y * e.z;
            if (volume > biggest)
            {
                biggest = volume;
                body = r;
            }
        }
        if (body == null || biggest <= 0f)
            return;
        Bounds bounds = body.bounds;

        Vector3 side = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized * Mathf.Sign(bearCuffAcross == 0f ? -1f : bearCuffAcross);
        Vector3 towardNurse = Vector3.ProjectOnPlane(cam.transform.position - bounds.center, Vector3.up).normalized;
        float size = bounds.size.y;

        Vector3 basePoint;
        Vector3 armDir;
        if (!(bearCuffFindArm && TryFindBearArm(body, bounds, side, out basePoint, out armDir)))
        {
            // Old placement from the bear's box.
            float halfWidth = Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z;
            float halfDepth = Mathf.Abs(towardNurse.x) * bounds.extents.x + Mathf.Abs(towardNurse.z) * bounds.extents.z;
            basePoint = bounds.center + side * (halfWidth * Mathf.Abs(bearCuffAcross)) + Vector3.up * (size * bearCuffUp) + towardNurse * (halfDepth * bearCuffFront);
            armDir = (side + Vector3.down * 0.35f).normalized;
        }

        bearCuff = NewCuff("Part9_BearCuff");
        bearCuff.localScale = new Vector3(size * bearCuffSize.x, size * bearCuffSize.y * 0.5f, size * bearCuffSize.x);
        Renderer cuffRenderer = bearCuff.GetComponent<Renderer>();
        if (cuffRenderer != null)
        {
            cuffRenderer.material.color = bearCuffColor;
            if (cuffRenderer.material.HasProperty("_EmissionColor"))
            {
                cuffRenderer.material.EnableKeyword("_EMISSION");
                cuffRenderer.material.SetColor("_EmissionColor", bearCuffColor * 0.45f);
            }
        }
        bearCuff.SetParent(bear, true);

        // Remember the spot in the bear's own space, so it moves with the bear and the
        // Nudge / Turn values can be changed while playing.
        bearCuffBear = bear;
        bearCuffBaseLocal = bear.InverseTransformPoint(basePoint);
        bearCuffArmLocal = bear.InverseTransformDirection(armDir);
        bearCuffSideLocal = bear.InverseTransformDirection(side);
        bearCuffTowardLocal = bear.InverseTransformDirection(towardNurse);
        PlaceBearCuff();
        Debug.Log($"[Part9] Bear cuff on '{bear.name}' at {basePoint:F3}, arm direction {armDir:F2}.", bearCuff);
    }

    private void PlaceBearCuff()
    {
        if (bearCuff == null || bearCuffBear == null)
            return;
        Transform bear = bearCuffBear;
        Vector3 side = bear.TransformDirection(bearCuffSideLocal);
        Vector3 toward = bear.TransformDirection(bearCuffTowardLocal);
        Vector3 arm = bear.TransformDirection(bearCuffArmLocal);
        Vector3 position = bear.TransformPoint(bearCuffBaseLocal) + side * bearCuffNudge.x + Vector3.up * bearCuffNudge.y + toward * bearCuffNudge.z;
        arm = Quaternion.AngleAxis(bearCuffTurn, toward) * arm;
        bearCuff.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, arm.normalized));
    }

    // The arm = mesh points at chest height that stick out furthest to `side`. Returns a point on
    // the arm (between where it leaves the body and the paw) and the direction along the arm.
    private bool TryFindBearArm(Renderer body, Bounds bounds, Vector3 side, out Vector3 point, out Vector3 armDir)
    {
        point = Vector3.zero;
        armDir = side;
        Mesh mesh = null;
        Matrix4x4 toWorld = body.transform.localToWorldMatrix;
        if (body is SkinnedMeshRenderer skinned && skinned.sharedMesh != null && skinned.sharedMesh.isReadable)
        {
            mesh = new Mesh();
            skinned.BakeMesh(mesh);
            toWorld = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
        }
        else if (body is MeshRenderer)
        {
            MeshFilter filter = body.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                mesh = filter.sharedMesh;
        }
        if (mesh == null)
        {
            Debug.LogWarning("[Part9] Bear mesh is not readable (enable Read/Write in its import settings): using the box placement for the cuff.", body);
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        if (vertices.Length < 20)
            return false;
        float bottom = bounds.min.y, height = Mathf.Max(1e-4f, bounds.size.y);
        Vector3 centre = bounds.center;

        // Points in the arm band (chest height), measured sideways from the bear's middle.
        float maxOut = float.MinValue;
        List<Vector3> band = new List<Vector3>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 w = toWorld.MultiplyPoint3x4(vertices[i]);
            float h = (w.y - bottom) / height;
            if (h < 0.3f || h > 0.72f)
                continue;
            float o = Vector3.Dot(w - centre, side);
            if (o <= 0f)
                continue;
            band.Add(w);
            if (o > maxOut)
                maxOut = o;
        }
        if (band.Count < 6 || maxOut <= 0f)
            return false;

        // Paw = the outermost points; shoulder = where the arm still is but closer to the body.
        Vector3 paw = Vector3.zero, root = Vector3.zero;
        int pawCount = 0, rootCount = 0;
        foreach (Vector3 w in band)
        {
            float o = Vector3.Dot(w - centre, side) / maxOut;
            if (o > 0.85f) { paw += w; pawCount++; }
            else if (o > 0.55f && o < 0.7f) { root += w; rootCount++; }
        }
        if (pawCount == 0 || rootCount == 0)
            return false;
        paw /= pawCount;
        root /= rootCount;
        armDir = (paw - root).sqrMagnitude > 1e-8f ? (paw - root).normalized : side;
        point = Vector3.Lerp(root, paw, bearCuffAlongArm);
        return true;
    }

    private void HideBearCuff()
    {
        if (bearCuff != null)
            Destroy(bearCuff.gameObject);
        bearCuff = null;
    }

    private void ShowYayaCuff()
    {
        HideYayaCuff();
        if (yayaAnimation == null)
            return;
        cuffArm = MixamoHumanoidAvatarBuilder.FindBone(yayaAnimation.transform, "LeftArm");
        cuffForeArm = MixamoHumanoidAvatarBuilder.FindBone(yayaAnimation.transform, "LeftForeArm");
        if (cuffArm == null || cuffForeArm == null)
            return;
        yayaCuff = NewCuff("Part9_YayaCuff");
        cuffInflate = 0f;
    }

    private void HideYayaCuff()
    {
        if (yayaCuff != null)
            Destroy(yayaCuff.gameObject);
        yayaCuff = null;
    }

    // ---------------------------------------------------------------------------------
    // Blood pressure machine on its stand beside Yaya, a reading screen on it, a tube to the cuff
    // ---------------------------------------------------------------------------------
    // Puts the machine in its fixed place if it is not there yet. Returns true when it stands.
    public bool EnsureMachine()
    {
        if (machine != null)
            return true;
        ResolveReferences();
        ShowMachine();
        return machine != null;
    }

    // Auto: the machine is put in the room shortly after the scene starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PlaceMachineAtStart()
    {
        PediatricVitalSignsPart9Flow flow = FindObjectOfType<PediatricVitalSignsPart9Flow>(true);
        if (flow == null || !flow.machineFromStart || !flow.showMachineTrolley)
            return;
        new GameObject("Part9_MachinePlacer (auto)").AddComponent<Part9MachinePlacer>().Setup(flow);
    }

    private void ResetMachineScreen()
    {
        if (machineValueText != null)
            machineValueText.text = "--";
    }

    private void ShowMachine()
    {
        HideMachine();
        if (!showMachineTrolley || yayaAnimation == null)
            return;

        Transform yaya = yayaAnimation.transform;
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(yaya, "Hips") ?? yaya;
        Transform arm = MixamoHumanoidAvatarBuilder.FindBone(yaya, "LeftArm");
        Vector3 centre = hips.position;
        // Toward the room = the way Yaya faces when she sits (not where the nurse happens to stand,
        // so the machine is always in the same place).
        Vector3 towardNurse = Vector3.ProjectOnPlane(PrefabPerformanceRuntime.GetActorPelvisForward(yaya), Vector3.up);
        if (towardNurse.sqrMagnitude < 1e-4f)
            towardNurse = Vector3.ProjectOnPlane(yaya.forward, Vector3.up);
        if (towardNurse.sqrMagnitude < 1e-4f)
            towardNurse = Vector3.forward;
        towardNurse.Normalize();
        Vector3 side = arm != null ? Vector3.ProjectOnPlane(arm.position - centre, Vector3.up) : Vector3.Cross(Vector3.up, towardNurse);
        if (side.sqrMagnitude < 1e-4f)
            side = Vector3.Cross(Vector3.up, towardNurse);
        side.Normalize();

        Vector3 spot;
        Quaternion facing;
        GameObject marker = FindSceneObjectByName(machineSpotName);
        if (marker != null)
        {
            // Placed by hand in the scene: an empty object named "BP_Machine_Spot"
            // (its blue arrow = the way the screen faces).
            spot = marker.transform.position;
            Vector3 markerForward = Vector3.ProjectOnPlane(marker.transform.forward, Vector3.up);
            towardNurse = markerForward.sqrMagnitude > 1e-4f ? markerForward.normalized : towardNurse;
            facing = Quaternion.LookRotation(towardNurse, Vector3.up) * Quaternion.Euler(0f, trolleyYawOffset + machineModelYaw, 0f);
        }
        else
        {
            // Right beside Yaya, on the side away from Mom, a little toward the room
            // (measured from Yaya, not from the bed, whose collider can reach the window).
            Vector3 across = Vector3.Cross(Vector3.up, towardNurse).normalized;
            Vector3 away = across;
            if (TryGetMomPosition(out Vector3 momAt))
            {
                if (Vector3.Dot(momAt - centre, across) > 0f)
                    away = -across;
            }
            else if (Vector3.Dot(side, across) > 0f)
            {
                away = -across;
            }
            spot = centre + away * machineBesideYaya + towardNurse * machineTowardRoom;
            // Screen toward where the nurse stands in front of Yaya.
            Vector3 nurseSpot = centre + towardNurse * 1.4f;
            Vector3 look = Vector3.ProjectOnPlane(nurseSpot - spot, Vector3.up);
            look = look.sqrMagnitude > 1e-4f ? look.normalized : towardNurse;
            facing = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, trolleyYawOffset + machineModelYaw, 0f);
            towardNurse = look;
        }
        spot.y = FindFloorY(spot, centre);

        machine = new GameObject("Part9_BloodPressureMachine");
        machine.transform.SetPositionAndRotation(spot, facing);

        // Optional trolley.
        GameObject trolley = useTrolley ? SpawnResource(trolleyResource, machine.transform) : null;

        // The machine: the real prefab at its real size, or a small box scaled to Monitor Width.
        GameObject monitor = SpawnResource(machineResource, machine.transform);
        if (monitor == null)
        {
            monitor = BuildMonitorBox(machine.transform);
            Bounds boxBounds = RendererBounds(monitor);
            float biggestSide = Mathf.Max(boxBounds.size.x, Mathf.Max(boxBounds.size.y, boxBounds.size.z));
            if (biggestSide > 1e-4f)
                monitor.transform.localScale *= monitorWidth / biggestSide;
        }
        Bounds monitorBounds = RendererBounds(monitor);
        float standOn = spot.y;
        if (trolley != null)
            standOn = RendererBounds(trolley).max.y;
        monitor.transform.position += new Vector3(spot.x - monitorBounds.center.x, standOn - monitorBounds.min.y, spot.z - monitorBounds.center.z);
        monitorBounds = RendererBounds(monitor);
        machineTopLocal = machine.transform.InverseTransformPoint(new Vector3(spot.x, monitorBounds.max.y, spot.z)).y;
        machineToward = towardNurse;

        // Reading on the front of the monitor (same numbers as the card in front of the eyes).
        GameObject screen = new GameObject("Screen", typeof(RectTransform));
        screen.transform.SetParent(machine.transform, false);
        Canvas canvas = screen.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform screenRect = (RectTransform)screen.transform;
        screenRect.sizeDelta = new Vector2(240f, 110f);
        machineScreen = screen.transform;
        PlaceMachineScreen();
        TMP_FontAsset font = nursePromptText != null ? nursePromptText.font : null;
        NewImage(screenRect, "Back", new Color(0.08f, 0.16f, 0.15f, 0.95f), Vector2.zero, new Vector2(240f, 110f)).raycastTarget = false;
        NewText(screenRect, "Title", font, "血壓計 mmHg", 18f, new Color(0.75f, 0.85f, 0.82f), new Vector2(0f, 36f), new Vector2(230f, 26f));
        machineValueText = NewText(screenRect, "Value", font, "--", 46f, Color.white, new Vector2(0f, -10f), new Vector2(230f, 64f));

        // Tube from the monitor's side to the cuff.
        machineTubeStart = new GameObject("TubeStart").transform;
        machineTubeStart.SetParent(machine.transform, false);
        machineTubeStart.position = new Vector3(spot.x, monitorBounds.max.y - Mathf.Min(0.2f, monitorBounds.size.y * 0.3f), spot.z) + side * 0.06f;
        machineTube = machine.AddComponent<LineRenderer>();
        machineTube.useWorldSpace = true;
        machineTube.positionCount = 12;
        machineTube.widthMultiplier = 0.012f;
        machineTube.numCapVertices = 3;
        machineTube.material = new Material(Shader.Find("Sprites/Default"));
        machineTube.startColor = tubeColor;
        machineTube.endColor = tubeColor;
        machineTube.enabled = false;

        Debug.Log($"[Part9] Blood pressure machine at {spot:F2} ('{monitor.name}', height {monitorBounds.size.y:F2} m, trolley {(trolley != null)}).", machine);
    }

    // Where Mom stands (her visible body if she has been swapped for the new model).
    private bool TryGetMomPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (MomSkinSwap.Active != null && MomSkinSwap.Active.IsShown)
        {
            position = MomSkinSwap.Active.ChestPosition;
            return true;
        }
        if (momAnimation == null)
            momAnimation = FindObjectOfType<MomAnimationPlayer>();
        if (momAnimation == null)
            return false;
        Transform hips = MixamoHumanoidAvatarBuilder.FindBone(momAnimation.transform, "Hips");
        position = hips != null ? hips.position : momAnimation.transform.position;
        return true;
    }

    // The bed Yaya sits on: the collider straight under her hips.
    private bool TryGetBed(Vector3 hips, out Bounds bed)
    {
        bed = default;
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(hips + Vector3.up * 0.3f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || (yayaAnimation != null && hit.collider.transform.IsChildOf(yayaAnimation.transform)))
                continue;
            Bounds b = hit.collider.bounds;
            // A bed: longer than 1 m one way, not the floor (which is much bigger).
            float longest = Mathf.Max(b.size.x, b.size.z);
            if (longest < 1f || longest > 4f)
                continue;
            float drop = hips.y - hit.point.y;
            if (drop >= -0.05f && drop < best)
            {
                best = drop;
                bed = b;
                found = true;
            }
        }
        return found;
    }

    // The floor under `spot`: where Mom stands (her feet are on the floor), else the lowest
    // surface found straight below, else a bit below Yaya's hips.
    private float FindFloorY(Vector3 spot, Vector3 yayaHips)
    {
        if (momAnimation == null)
            momAnimation = FindObjectOfType<MomAnimationPlayer>();
        if (momAnimation != null)
        {
            Transform foot = MixamoHumanoidAvatarBuilder.FindBone(momAnimation.transform, "LeftToeBase")
                ?? MixamoHumanoidAvatarBuilder.FindBone(momAnimation.transform, "LeftFoot");
            float y = momAnimation.transform.position.y;
            if (foot != null)
                y = Mathf.Min(y, foot.position.y);
            if (y < yayaHips.y - 0.2f)
                return y;
        }
        float lowest = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(spot + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.point.y < yayaHips.y - 0.2f && hit.point.y < lowest)
                lowest = hit.point.y;
        }
        if (lowest < float.MaxValue)
            return lowest;
        return yayaHips.y - 0.7f;
    }

    // A simple blood pressure monitor: white body, dark screen frame, a grey handle on top.
    private GameObject BuildMonitorBox(Transform parent)
    {
        GameObject root = new GameObject("BP_Monitor (built-in)");
        root.transform.SetParent(parent, false);
        Part(root.transform, "Body", new Vector3(0f, 0.11f, 0f), new Vector3(0.30f, 0.22f, 0.12f), new Color(0.93f, 0.94f, 0.96f));
        Part(root.transform, "ScreenFrame", new Vector3(0f, 0.12f, 0.061f), new Vector3(0.24f, 0.15f, 0.004f), new Color(0.12f, 0.14f, 0.16f));
        Part(root.transform, "Handle", new Vector3(0f, 0.235f, 0f), new Vector3(0.18f, 0.02f, 0.03f), new Color(0.6f, 0.62f, 0.66f));
        Part(root.transform, "Button", new Vector3(0.105f, 0.03f, 0.061f), new Vector3(0.03f, 0.02f, 0.006f), new Color(0.25f, 0.6f, 0.95f));
        return root;
    }

    private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().material.color = color;
    }

    // The reading screen near the top of the machine, facing the nurse.
    private void PlaceMachineScreen()
    {
        if (machineScreen == null || machine == null)
            return;
        Transform root = machine.transform;
        Vector3 top = root.position + Vector3.up * machineTopLocal;
        Vector3 right = Vector3.Cross(Vector3.up, machineToward).normalized;
        machineScreen.position = top - Vector3.up * machineScreenOffset.y + machineToward * machineScreenOffset.z - right * machineScreenOffset.x;
        machineScreen.rotation = Quaternion.LookRotation(-machineToward, Vector3.up);
        machineScreen.localScale = Vector3.one * (Mathf.Max(0.05f, machineScreenWidth) / 240f);
    }

    private void HideMachine()
    {
        if (machine != null)
            Destroy(machine);
        machine = null;
        machineTube = null;
        machineTubeStart = null;
        machineValueText = null;
        machineScreen = null;
    }

    // A sagging tube from the machine to whichever cuff is on (bear first, then Yaya).
    private void UpdateMachineTube()
    {
        if (machineTube == null || machineTubeStart == null)
            return;
        Transform cuff = yayaCuff != null ? yayaCuff : bearCuff;
        if (cuff == null)
        {
            machineTube.enabled = false;
            return;
        }
        machineTube.enabled = true;
        Vector3 a = machineTubeStart.position;
        Vector3 b = cuff.position;
        float sag = Mathf.Clamp(Vector3.Distance(a, b) * 0.35f, 0.05f, 0.35f);
        for (int i = 0; i < machineTube.positionCount; i++)
        {
            float t = i / (float)(machineTube.positionCount - 1);
            machineTube.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t)));
        }
    }

    private static GameObject SpawnResource(string resource, Transform parent)
    {
        if (string.IsNullOrEmpty(resource))
            return null;
        GameObject prefab = Resources.Load<GameObject>(resource);
        if (prefab == null)
        {
            Debug.LogWarning($"[Part9] Resources/{resource}.prefab not found: using a simple box instead.");
            return null;
        }
        GameObject go = Instantiate(prefab, parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
            c.enabled = false;
        return go;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ---------------------------------------------------------------------------------
    // Measuring
    // ---------------------------------------------------------------------------------
    private IEnumerator MeasureBloodPressure()
    {
        if (randomReading)
        {
            systolic = UnityEngine.Random.Range(Mathf.Min(systolicRange.x, systolicRange.y), Mathf.Max(systolicRange.x, systolicRange.y) + 1);
            diastolic = UnityEngine.Random.Range(Mathf.Min(diastolicRange.x, diastolicRange.y), Mathf.Max(diastolicRange.x, diastolicRange.y) + 1);
            diastolic = Mathf.Min(diastolic, systolic - 25);
            Debug.Log($"[Part9] Yaya's blood pressure this run: {systolic}/{diastolic} mmHg", this);
        }
        ShowYayaCuff();
        ShowReadingCard();
        Color grey = new Color(0.85f, 0.85f, 0.85f);

        // Filling: the number climbs, the cuff swells.
        float top = systolic + 30f;
        for (float t = 0f; t < inflateSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.1f, inflateSeconds));
            cuffInflate = Mathf.SmoothStep(0f, 1f, k);
            SetReading(Mathf.RoundToInt(Mathf.Lerp(0f, top, k)).ToString(), "充氣中…", grey);
            yield return null;
        }

        // Letting the air out slowly while it measures.
        for (float t = 0f; t < deflateSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.1f, deflateSeconds));
            cuffInflate = 1f - k;
            SetReading(Mathf.RoundToInt(Mathf.Lerp(top, diastolic - 8f, k)).ToString(), "測量中…", grey);
            yield return null;
        }

        cuffInflate = 0f;
        SetReading(systolic + " / " + diastolic, "測量完成", new Color(0.45f, 0.9f, 0.6f));
        yield return new WaitForSeconds(2.5f);
        HideReadingCard();
    }

    private void SetReading(string value, string status, Color statusColor)
    {
        if (readingValueText != null)
            readingValueText.text = value;
        if (machineValueText != null)
            machineValueText.text = value;
        if (readingStatusText != null)
        {
            readingStatusText.text = status;
            readingStatusText.color = statusColor;
        }
    }

    private void ShowReadingCard()
    {
        if (readingCard == null)
        {
            readingCard = new GameObject("Part9_BloodPressureCard", typeof(RectTransform));
            Canvas canvas = readingCard.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            readingCard.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            RectTransform root = (RectTransform)readingCard.transform;
            root.sizeDelta = new Vector2(300f, 190f);
            TMP_FontAsset font = nursePromptText != null ? nursePromptText.font : null;
            NewImage(root, "Background", new Color(0.11f, 0.13f, 0.16f, 0.92f), Vector2.zero, new Vector2(300f, 190f)).raycastTarget = false;
            NewImage(root, "Screen", new Color(0.16f, 0.32f, 0.30f, 1f), new Vector2(0f, 6f), new Vector2(260f, 92f)).raycastTarget = false;
            NewText(root, "Title", font, "血壓計", 24f, new Color(0.75f, 0.78f, 0.8f), new Vector2(0f, 74f), new Vector2(280f, 32f));
            readingValueText = NewText(root, "Value", font, "0", 54f, Color.white, new Vector2(0f, 8f), new Vector2(260f, 86f));
            NewText(root, "Unit", font, "mmHg", 18f, new Color(0.75f, 0.78f, 0.8f), new Vector2(0f, -50f), new Vector2(280f, 24f));
            readingStatusText = NewText(root, "Status", font, "充氣中…", 24f, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, -76f), new Vector2(280f, 32f));
        }
        readingCard.SetActive(true);
    }

    private void HideReadingCard()
    {
        if (readingCard != null)
            readingCard.SetActive(false);
        if (pictureCard == null || !pictureCard.activeSelf)
            RestoreHud();
    }

    // ---------------------------------------------------------------------------------
    // Picture of the cuff and the artery
    // ---------------------------------------------------------------------------------
    private void ShowPicture(bool answer)
    {
        Texture2D picture = Resources.Load<Texture2D>(answer ? answerPictureResource : questionPictureResource);
        if (picture == null)
        {
            Debug.LogWarning("[Part9] Picture not found in Resources: " + (answer ? answerPictureResource : questionPictureResource), this);
            pictureAnswered = true;   // do not block the story
            return;
        }

        if (pictureCard == null)
        {
            pictureCard = new GameObject("Part9_CuffArteryPicture", typeof(RectTransform));
            Canvas canvas = pictureCard.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 45;
            pictureCard.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            pictureImage = pictureCard.AddComponent<RawImage>();
            pictureImage.raycastTarget = false;

            // Ring that follows the pointer onto a choice.
            GameObject ring = new GameObject("ChoiceRing", typeof(RectTransform));
            ring.transform.SetParent(pictureCard.transform, false);
            choiceRing = (RectTransform)ring.transform;
            Image ringImage = ring.AddComponent<Image>();
            ringImage.color = new Color(1f, 1f, 1f, 0.45f);
            ringImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");   // round
            ringImage.raycastTarget = false;
        }

        RectTransform root = (RectTransform)pictureCard.transform;
        root.sizeDelta = new Vector2(700f, 700f * picture.height / Mathf.Max(1f, picture.width));
        pictureImage.texture = picture;
        pictureIsAnswer = answer;
        if (choiceRing != null)
            choiceRing.gameObject.SetActive(false);
        pictureCard.SetActive(true);
    }

    // Clicking one of the three spots on the question picture (mouse, or controller ray + trigger).
    private void UpdatePictureClick()
    {
        if (pictureIsAnswer || pictureAnswered || armChoiceSpots == null)
            return;

        RectTransform root = (RectTransform)pictureCard.transform;
        Vector2 size = root.sizeDelta;
        Plane plane = new Plane(root.forward, root.position);
        int hovered = -1;

        void Test(Ray ray)
        {
            if (hovered >= 0 || !plane.Raycast(ray, out float distance) || distance > 8f)
                return;
            Vector3 local = root.InverseTransformPoint(ray.GetPoint(distance));
            for (int i = 0; i < armChoiceSpots.Length; i++)
            {
                Vector2 spot = new Vector2((armChoiceSpots[i].x - 0.5f) * size.x, (0.5f - armChoiceSpots[i].y) * size.y);
                if (Vector2.Distance(new Vector2(local.x, local.y), spot) <= choiceRadius * size.x)
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
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (cam != null && (Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive))
        {
            Test(cam.ScreenPointToRay(Input.mousePosition));
            if (Input.GetMouseButtonDown(0))
                clicked = true;
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

        // Ring on the choice under the pointer (red for a moment on a wrong pick).
        int shown = hovered >= 0 ? hovered : (Time.unscaledTime < wrongUntil ? wrongChoice : -1);
        if (choiceRing != null)
        {
            choiceRing.gameObject.SetActive(shown >= 0);
            if (shown >= 0)
            {
                float d = choiceRadius * size.x * 2.2f;
                choiceRing.sizeDelta = new Vector2(d, d);
                choiceRing.anchoredPosition = new Vector2((armChoiceSpots[shown].x - 0.5f) * size.x, (0.5f - armChoiceSpots[shown].y) * size.y);
                bool wrong = shown == wrongChoice && Time.unscaledTime < wrongUntil;
                choiceRing.GetComponent<Image>().color = wrong ? new Color(1f, 0.25f, 0.2f, 0.55f) : new Color(1f, 1f, 1f, 0.45f);
            }
        }

        if (!clicked || hovered < 0)
            return;

        if (hovered == correctChoice)
        {
            pictureAnswered = true;
            PartScoreBoard.ReportAttempt("arm", true);
            Debug.Log("[Part9] Right spot chosen.", this);
        }
        else
        {
            wrongChoice = hovered;
            wrongUntil = Time.unscaledTime + 0.9f;
            PartScoreBoard.ReportAttempt("arm", false);
            if (nursePromptText != null && !string.IsNullOrEmpty(wrongChoiceText))
                nursePromptText.text = wrongChoiceText;
        }
    }

    private void HidePicture()
    {
        if (pictureCard != null)
            pictureCard.SetActive(false);
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
    // ---------------------------------------------------------------------------------
    // Helpers (same as the other parts)
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
            Debug.LogWarning("[Part9] Nurse prompt text not found.", this);

        waitingForNurseAction = waitingAction;
    }

    private void ShowQuiz()
    {
        ResolveReferences();
        SetPanelVisible(nursePromptPanel, false, "TopHint_Panel");
        SetPromptSkipButtonsVisible(false);
        SetPanelVisible(dialoguePanel, false, dialoguePanelChildName);
        part3VisualDemo?.TemporarilyHideStickerForQuiz();
        ApplyQuizScale();
        SetPanelVisible(quizPanel, true, quizPanelChildName);
        ApplyQuizScale();

        QuizPanelRuntimeHelper.BeginQuiz(quizPanel, quizPanelChildName);
        if (quizCompletionRoutine != null)
            StopCoroutine(quizCompletionRoutine);
        quizCompletionRoutine = StartCoroutine(WaitForQuizPanelClosed());
    }

    // Quiz 8 is laid out bigger than the other quizzes: show it at a fixed, smaller size
    // (set before it appears, so it does not pop).
    private RectTransform scaledQuizPanel;
    private Vector3 quizPanelOriginalScale;

    private void ApplyQuizScale()
    {
        if (quizPanel == null)
            return;
        RectTransform panel = FindDeepChild(quizPanel.transform, quizPanelChildName) as RectTransform;
        if (panel == null)
            return;
        if (scaledQuizPanel != panel)
        {
            scaledQuizPanel = panel;
            quizPanelOriginalScale = panel.localScale;
        }
        panel.localScale = quizPanelOriginalScale * Mathf.Clamp(quizPanelSize, 0.2f, 2f);
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
        Debug.Log("[Part9] Quiz closed.", this);
        onQuizClosed?.Invoke();
    }

    private void HandleBackendJson(string json)
    {
        if (string.Equals(json, lastProcessedBackendJson, StringComparison.Ordinal))
            return;

        lastProcessedBackendJson = json;
        string speechText = ExtractBackendSpeechText(json);
        if (string.IsNullOrWhiteSpace(speechText))
            return;

        string keywords;
        switch (waitingForNurseAction)
        {
            case WaitingForNurseAction.BearDemo: keywords = bearDemoKeywords; break;
            case WaitingForNurseAction.PraiseSticker: keywords = praiseStickerKeywords; break;
            default: keywords = string.Empty; break;
        }

        if (ContainsAnyKeyword(speechText, keywords))
        {
            // Yaya is in meltdown (red): the nurse must calm her before the story moves on.
            if (KidEmotionGate.Blocking)
            {
                Debug.Log("[Part9] Keywords matched, but Yaya must be calmed first.", this);
                return;
            }

            Debug.Log($"[Part9] Backend matched {waitingForNurseAction}. text={speechText}", this);
            waitingForNurseAction = WaitingForNurseAction.None;
            return;
        }

        Debug.Log($"[Part9] Waiting for {waitingForNurseAction}, speech did not match. text={speechText}", this);
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
        HidePicture();
        HideReadingCard();
        HideBearCuff();
        HideYayaCuff();
        if (part3VisualDemo != null && part3VisualDemo.IsStickerGiveActive)
            part3VisualDemo.EndStickerGive();
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
            Debug.LogWarning("[Part9] Failed to parse backend json: " + e.Message);
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

// Puts the blood pressure machine in its fixed place a moment after the scene starts
// (once Yaya sits on the bed), then removes itself.
public class Part9MachinePlacer : MonoBehaviour
{
    private PediatricVitalSignsPart9Flow flow;
    private float startTime;

    public void Setup(PediatricVitalSignsPart9Flow f)
    {
        flow = f;
        startTime = Time.time;
    }

    private void Update()
    {
        if (flow == null || Time.time - startTime > 30f)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time - startTime < 1f)
            return;
        if (flow.EnsureMachine())
            Destroy(gameObject);
    }
}
