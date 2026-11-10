using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

// End screen shown when the whole story (Part 1-9) is finished.
//
//   left   : a ring that fills while the total score counts up, then up to three stars
//   right  : question score, tone score and "right at the first try" counting up one after
//            the other, one coloured tile per quiz / hands-on task, and a short comment
//   bottom : whether the result reached the backend, and a "try again" button that goes
//            back to the login screen (the scene is loaded again)
//
// Opened by PartScoreBoard when the story ends. Nothing has to be placed in the scene; put
// the component on an object in the scene only if you want to change the values below.
public class ScenarioEndScreen : MonoBehaviour
{
    [Header("Where it appears")]
    [Tooltip("Seconds after the blood pressure is done before the screen opens.")]
    [SerializeField] private float openDelay = 1.2f;
    [Tooltip("Distance in front of the eyes (m).")]
    [SerializeField] private float distance = 1.05f;
    [Tooltip("Width of the screen (m).")]
    [SerializeField] private float width = 0.88f;
    [Tooltip("Up (+) / down (-) from eye height (m).")]
    [SerializeField] private float heightOffset = -0.04f;
    [Tooltip("The screen moves back in front of you when you turn away more than this (degrees).")]
    [SerializeField] private float followBeyondDegrees = 45f;

    [Header("Counting")]
    [SerializeField] private float totalCountSeconds = 1.8f;
    [SerializeField] private float rowCountSeconds = 0.8f;
    [Tooltip("Total score needed for 1, 2 and 3 stars.")]
    [SerializeField] private Vector3Int starScores = new Vector3Int(60, 75, 90);

    [Header("Texts")]
    [SerializeField] private string titleText = "測驗完成";
    [SerializeField] private string subtitleText = "芽芽的生命徵象都量好了，辛苦了！";
    [SerializeField] private string retryText = "再測驗一次";
    [SerializeField] private string retryNoteText = "再測驗一次會回到登入畫面　結束會關閉程式";
    [SerializeField] private string quitText = "結束";

    private const float W = 760f, H = 540f;

    private static readonly Color Panel = new Color(0.09f, 0.11f, 0.15f, 0.96f);
    private static readonly Color Card = new Color(0.15f, 0.18f, 0.23f, 1f);
    private static readonly Color Track = new Color(0.24f, 0.28f, 0.34f, 1f);
    private static readonly Color Soft = new Color(0.72f, 0.77f, 0.83f, 1f);
    private static readonly Color Good = new Color(0.36f, 0.84f, 0.56f, 1f);
    private static readonly Color Fair = new Color(0.98f, 0.76f, 0.30f, 1f);
    private static readonly Color Bad = new Color(0.95f, 0.42f, 0.38f, 1f);
    private static readonly Color Accent = new Color(0.38f, 0.72f, 0.98f, 1f);
    private static readonly Color StarOff = new Color(0.30f, 0.34f, 0.40f, 1f);

    private static ScenarioEndScreen instance;
    private static bool rebootstrapHooked;

    private RectTransform root;
    private CanvasGroup group;
    private Image ringFill;
    private TMP_Text totalNumber, gradeText;
    private readonly Image[] stars = new Image[3];
    private TMP_Text questionValue, toneValue, firstTryValue;
    private Image questionBar, toneBar, firstTryBar;
    private readonly List<RectTransform> tiles = new List<RectTransform>();
    private TMP_Text commentText, uploadText, retryLabel;
    private RectTransform retryButton;
    private Image retryImage;
    private CanvasGroup retryGroup;

    private bool shown, retryReady, restarting, triggerWasDown;
    private float shownAt, nextHide, nextRayScan;
    private XRRayInteractor[] rayInteractors;
    private readonly List<UnityEngine.XR.InputDevice> xrDevices = new List<UnityEngine.XR.InputDevice>();

    // True from the moment the end screen is asked for until "try again" / "finish" is pressed.
    public static bool IsShowing => instance != null && instance.shown && !instance.restarting;

    // Called by PartScoreBoard when the story is finished.
    public static void Show()
    {
        if (instance == null)
            instance = FindObjectOfType<ScenarioEndScreen>(true);
        if (instance == null)
            instance = new GameObject("ScenarioEndScreen (auto)").AddComponent<ScenarioEndScreen>();
        instance.gameObject.SetActive(true);
        instance.enabled = true;
        if (!instance.shown)
            instance.StartCoroutine(instance.Run());
    }

    // ---------------------------------------------------------------------------------
    // Sequence
    // ---------------------------------------------------------------------------------
    private IEnumerator Run()
    {
        shown = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, openDelay));

        int question = Mathf.Clamp(PartScoreBoard.LastDetailedScore, 0, 80);
        int tone = Mathf.Clamp(PartScoreBoard.LastToneScore, 0, 20);
        int firstTry = Mathf.Clamp(PartScoreBoard.LastRightFirstTry, 0, 8);
        int total = question + tone;
        PartScoreBoard.Item[] items = PartScoreBoard.LastItems ?? new PartScoreBoard.Item[0];

        Build(items);
        HideEverythingElse();
        Place(true);
        shownAt = Time.unscaledTime;

        // Panel in.
        yield return Animate(0.35f, k =>
        {
            group.alpha = k;
            root.localScale = Vector3.one * (width / W) * Mathf.Lerp(0.9f, 1f, k);
        });

        // Total: ring and number run together.
        Color totalColor = ScoreColor(total / 100f);
        yield return Animate(totalCountSeconds, k =>
        {
            float value = total * k;
            totalNumber.text = Mathf.RoundToInt(value).ToString();
            ringFill.fillAmount = value / 100f;
            ringFill.color = Color.Lerp(Accent, totalColor, k);
        });
        totalNumber.text = total.ToString();
        totalNumber.color = totalColor;
        yield return Pop(totalNumber.rectTransform, 1.18f);

        // Stars, one after the other.
        int starCount = total >= starScores.z ? 3 : total >= starScores.y ? 2 : total >= starScores.x ? 1 : 0;
        for (int i = 0; i < starCount; i++)
        {
            stars[i].color = Fair;
            yield return Pop(stars[i].rectTransform, 1.5f);
        }
        gradeText.text = starCount >= 3 ? "表現優秀" : starCount == 2 ? "表現良好" : starCount == 1 ? "通過，還可以更好" : "再練習一次吧";
        gradeText.color = starCount >= 2 ? Good : starCount == 1 ? Fair : Bad;
        yield return Animate(0.3f, k => gradeText.alpha = k);

        // Rows.
        yield return CountRow(questionValue, questionBar, question, 80, "");
        yield return CountRow(toneValue, toneBar, tone, 20, "");
        yield return CountRow(firstTryValue, firstTryBar, firstTry, 8, " 題");

        // Tiles.
        foreach (RectTransform tile in tiles)
        {
            tile.gameObject.SetActive(true);
            yield return Pop(tile, 1.35f, 0.09f);
        }

        commentText.text = BuildComment(items, tone, total);
        yield return Animate(0.4f, k => commentText.alpha = k);

        retryGroup.gameObject.SetActive(true);
        yield return Animate(0.35f, k => retryGroup.alpha = k);
        retryReady = true;
    }

    private IEnumerator CountRow(TMP_Text value, Image bar, int score, int max, string unit)
    {
        Color color = ScoreColor(score / (float)max);
        yield return Animate(rowCountSeconds, k =>
        {
            float v = score * k;
            value.text = Mathf.RoundToInt(v) + " / " + max + unit;
            bar.fillAmount = v / max;
            bar.color = Color.Lerp(Accent, color, k);
        });
        value.text = score + " / " + max + unit;
        value.color = color;
    }

    // k runs 0 -> 1, fast at first and slowing down at the end (like the blood pressure reading).
    private static IEnumerator Animate(float seconds, Action<float> apply)
    {
        float start = Time.unscaledTime;
        seconds = Mathf.Max(0.01f, seconds);
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
            apply(1f - Mathf.Pow(1f - t, 3f));
            if (t >= 1f)
                yield break;
            yield return null;
        }
    }

    private static IEnumerator Pop(RectTransform target, float peak, float seconds = 0.22f)
    {
        yield return Animate(seconds, k => target.localScale = Vector3.one * Mathf.Lerp(peak, 1f, k));
        target.localScale = Vector3.one;
    }

    private static Color ScoreColor(float share)
    {
        return share >= 0.85f ? Good : share >= 0.6f ? Fair : Bad;
    }

    private static string BuildComment(PartScoreBoard.Item[] items, int tone, int total)
    {
        List<string> weak = new List<string>();
        List<string> weakQuizzes = new List<string>();
        foreach (PartScoreBoard.Item item in items)
        {
            if (item == null || (item.solved && item.wrongAttempts == 0))
                continue;
            if (item.isQuiz)
                weakQuizzes.Add(TileLabel(item.id));
            else
                weak.Add(item.id == "cuff" ? "壓脈帶尺寸" : item.id == "arm" ? "壓脈帶位置" : item.id);
        }
        if (weakQuizzes.Count > 0)
            weak.Insert(0, "第 " + string.Join("、", weakQuizzes) + " 題");

        string first = weak.Count > 0
            ? "可以再複習：" + string.Join("、", weak)
            : total >= 90 ? "每一題都一次答對，太棒了！" : "題目都答對了！";
        string second = tone >= 20
            ? "全程說話溫柔，芽芽很安心。"
            : "語氣被扣了 " + (20 - tone) + " 分，對孩子說話可以再溫柔一點。";
        return first + "\n" + second;
    }

    private static string TileLabel(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "?";
        if (id.StartsWith("quiz_"))
            return id.Substring(5);
        return id == "cuff" ? "帶" : id == "arm" ? "位" : id.Substring(0, 1);
    }

    // ---------------------------------------------------------------------------------
    // Every frame: stay in front of the nurse, upload state, the button
    // ---------------------------------------------------------------------------------
    private void LateUpdate()
    {
        if (root == null)
            return;

        Place(false);

        if (Time.unscaledTime >= nextHide)
        {
            nextHide = Time.unscaledTime + 0.5f;
            HideEverythingElse();
        }

        if (uploadText != null && !restarting)
        {
            StudentRunContext context = StudentRunContext.Current;
            if (!context.HasStudentRun && !context.ResultSubmitted)
            {
                uploadText.text = "未登入後端，成績不會上傳";
                uploadText.color = Soft;
            }
            else if (context.ResultSubmitted)
            {
                uploadText.text = "成績已上傳";
                uploadText.color = Good;
            }
            else if (context.IsSubmitting || Time.unscaledTime - shownAt < 14f)
            {
                uploadText.text = "成績上傳中…";
                uploadText.color = Soft;
            }
            else
            {
                uploadText.text = "成績尚未上傳：" + UploadProblem();
                uploadText.color = Fair;
            }
        }

        UpdateButton();
    }

    // Why the result is not at the backend yet, in a few words.
    private string UploadProblem()
    {
        if (uploader == null)
            uploader = FindObjectOfType<StudentResultUploader>();
        string status = uploader != null ? uploader.LastStatusMessage : null;
        if (string.IsNullOrEmpty(status))
            return "請確認後端連線";
        if (status.Contains("failed permanently"))
            return "有語音上傳失敗";
        if (status.Contains("waiting for"))
            return "還在等語音分析";
        if (status.Contains("URL is unavailable"))
            return "找不到後端位址";
        if (status.Contains("upload failed"))
            return "後端沒有接受 (" + uploader.LastResponseCode + ")";
        if (status.Contains("does not match") || status.Contains("missing"))
            return "後端回覆的格式不對";
        return status.Length > 40 ? status.Substring(0, 40) : status;
    }
    private StudentResultUploader uploader;

    private void Place(bool snap)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
        forward.Normalize();
        Vector3 wantedPosition = cam.transform.position + forward * distance + Vector3.up * heightOffset;
        Quaternion wantedRotation = Quaternion.LookRotation(forward, Vector3.up);

        if (snap)
        {
            root.position = wantedPosition;
            root.rotation = wantedRotation;
            following = false;
            return;
        }

        // Stays where it is while the nurse reads it; comes back in front when she turns away or walks off.
        Vector3 toCard = Vector3.ProjectOnPlane(root.position - cam.transform.position, Vector3.up);
        bool away = Vector3.Angle(forward, toCard) > followBeyondDegrees || toCard.magnitude > distance + 0.6f || toCard.magnitude < distance * 0.45f;
        if (away)
            following = true;
        if (!following)
            return;

        float step = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
        root.position = Vector3.Lerp(root.position, wantedPosition, step);
        root.rotation = Quaternion.Slerp(root.rotation, wantedRotation, step);
        if (Vector3.Distance(root.position, wantedPosition) < 0.02f)
            following = false;
    }
    private bool following;
    private bool forceRestart, forceIsQuit;
    private RectTransform quitButton;
    private Image quitImage;
    private TMP_Text quitLabel;
    private TMP_Text retryNote;

    // The story is over: the prompts, the record button, the sticker and the emotion bar go away.
    private void HideEverythingElse()
    {
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas != null && canvas.enabled && !canvas.transform.IsChildOf(transform))
                canvas.enabled = false;
        }
        foreach (MedicalRecordHudButton button in FindObjectsOfType<MedicalRecordHudButton>())
            HideRenderers(button.transform);
        Part3VisualDemoController demo = FindObjectOfType<Part3VisualDemoController>();
        if (demo != null && demo.HudSticker != null)
            HideRenderers(demo.HudSticker);
        KidEmotionPresenter presenter = FindObjectOfType<KidEmotionPresenter>();
        if (presenter != null && presenter.MeterRoot != null)
            HideRenderers(presenter.MeterRoot);
    }

    private static void HideRenderers(Transform parent)
    {
        foreach (Renderer r in parent.GetComponentsInChildren<Renderer>(true))
            r.forceRenderingOff = true;
    }

    private void UpdateButton()
    {
        if (retryButton == null || !retryGroup.gameObject.activeSelf)
            return;

        int hovered = -1;   // 0 = try again, 1 = finish
        Plane plane = new Plane(root.forward, root.position);
        Vector2 groupPosition = retryGroup.GetComponent<RectTransform>().anchoredPosition;
        RectTransform[] buttons = { retryButton, quitButton };

        void Test(Ray ray)
        {
            if (hovered >= 0 || !plane.Raycast(ray, out float hit) || hit > 8f)
                return;
            Vector3 local = root.InverseTransformPoint(ray.GetPoint(hit));
            for (int i = 0; i < buttons.Length; i++)
            {
                Vector2 half = buttons[i].sizeDelta * 0.5f + new Vector2(8f, 12f);   // a little easier to hit
                Vector2 centre = buttons[i].anchoredPosition + groupPosition;
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

        float ease = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        bool onRetry = hovered == 0 && !restarting, onQuit = hovered == 1 && !restarting;
        retryButton.localScale = Vector3.Lerp(retryButton.localScale, Vector3.one * (onRetry ? 1.07f : 1f), ease);
        quitButton.localScale = Vector3.Lerp(quitButton.localScale, Vector3.one * (onQuit ? 1.07f : 1f), ease);
        retryImage.color = onRetry ? Color.Lerp(Accent, Color.white, 0.25f) : Accent;
        quitImage.color = onQuit ? Color.Lerp(Track, Color.white, 0.3f) : Track;

        if (clicked && hovered >= 0 && retryReady && !restarting)
            StartCoroutine(Restart(hovered == 1));
    }

    // ---------------------------------------------------------------------------------
    // Try again: back to the login screen
    // ---------------------------------------------------------------------------------
    private IEnumerator Restart(bool quit)
    {
        restarting = true;
        StudentRunContext context = StudentRunContext.Current;
        TMP_Text label = quit ? quitLabel : retryLabel;
        // "Go on anyway" only counts for the button that was pressed twice.
        if (forceRestart && forceIsQuit != quit)
        {
            forceRestart = false;
            retryLabel.text = retryText;
            quitLabel.text = quitText;
        }

        // The result must have reached the backend before the scene is loaded again: loading
        // resets every score, and a result sent after that would be saved as 0 points.
        if (context.HasStudentRun && !context.ResultSubmitted && !forceRestart)
        {
            label.text = "上傳中…";
            float giveUp = Time.unscaledTime + 12f;
            while (!context.ResultSubmitted && Time.unscaledTime < giveUp)
                yield return null;

            if (!context.ResultSubmitted)
            {
                // Still not there: the nurse / teacher decides. A second press starts again anyway.
                forceRestart = true;
                forceIsQuit = quit;
                restarting = false;
                label.text = quit ? "仍要結束" : "仍要重新開始";
                if (retryNote != null)
                {
                    retryNote.text = "成績還沒上傳成功，再按一次會放棄這筆成績";
                    retryNote.color = Fair;
                }
                yield break;
            }
        }

        // Giving up on a result that was never frozen: make sure nothing is sent after the
        // scores were reset (a frozen result is kept on disk with the right numbers).
        if (!context.ResultSubmitted && !context.HasPendingResult)
            context.ScenarioCompleted = false;

        if (quit)
        {
            label.text = "再見！";
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[ScenarioEndScreen] Finish: closing the application.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            yield break;
        }

        label.text = "準備中…";

        HookRebootstrap();
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log("[ScenarioEndScreen] Try again: loading the scene again -> " + scene.name);
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }
    }

    // The helper objects named "... (auto)" are made once when the game starts. They are gone
    // after the scene is loaded again, so they are made again here.
    private static readonly string[] AutoTypes =
    {
        "KidEmotionPresenter", "CharacterLook", "MomSkinSwap", "PartScoreBoard", "RoomFillLight",
        "FaceShapeTweak", "HudLayoutAdjuster", "MedicalRecordAvatar"
    };

    private static void HookRebootstrap()
    {
        if (rebootstrapHooked)
            return;
        rebootstrapHooked = true;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (mode != LoadSceneMode.Single)
                return;
            Assembly assembly = typeof(ScenarioEndScreen).Assembly;
            foreach (string name in AutoTypes)
            {
                try
                {
                    Type type = assembly.GetType(name);
                    if (type == null)
                        continue;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.GetParameters().Length == 0 &&
                            method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                            method.Invoke(null, null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ScenarioEndScreen] Could not restart " + name + ": " + e.Message);
                }
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // Building the screen
    // ---------------------------------------------------------------------------------
    private void Build(PartScoreBoard.Item[] items)
    {
        if (root != null)
            return;

        GameObject canvasObject = new GameObject("EndScreen_Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        root = (RectTransform)canvasObject.transform;
        root.sizeDelta = new Vector2(W, H);
        root.localScale = Vector3.one * (width / W);

        TMP_FontAsset font = FindChineseFont();
        Sprite ring = MakeRing();
        Sprite star = MakeStar();

        NewImage(root, "Background", Panel, Vector2.zero, new Vector2(W, H));
        NewImage(root, "TopLine", Accent, new Vector2(0f, H * 0.5f - 3f), new Vector2(W, 6f));

        NewText(root, "Title", font, titleText, 40f, Color.white, new Vector2(0f, 222f), new Vector2(W - 40f, 50f));
        string student = StudentRunContext.Current.StudentId;
        string subtitle = string.IsNullOrWhiteSpace(student) ? subtitleText : "學號 " + student + "　" + subtitleText;
        NewText(root, "Subtitle", font, subtitle, 20f, Soft, new Vector2(0f, 184f), new Vector2(W - 40f, 28f));

        // ---- left: total ----
        Vector2 ringCentre = new Vector2(-232f, 36f);
        NewImage(root, "TotalCard", Card, new Vector2(-232f, 6f), new Vector2(256f, 312f));
        Image ringBack = NewImage(root, "RingBack", Track, ringCentre, new Vector2(196f, 196f));
        ringBack.sprite = ring;
        ringFill = NewImage(root, "RingFill", Accent, ringCentre, new Vector2(196f, 196f));
        ringFill.sprite = ring;
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.fillAmount = 0f;
        NewText(root, "TotalLabel", font, "總分", 20f, Soft, ringCentre + new Vector2(0f, 50f), new Vector2(140f, 26f));
        totalNumber = NewText(root, "TotalNumber", font, "0", 68f, Color.white, ringCentre + new Vector2(0f, 2f), new Vector2(170f, 80f));
        NewText(root, "TotalMax", font, "/ 100", 18f, Soft, ringCentre + new Vector2(0f, -46f), new Vector2(140f, 24f));

        for (int i = 0; i < 3; i++)
        {
            stars[i] = NewImage(root, "Star" + (i + 1), StarOff, new Vector2(-232f + (i - 1) * 50f, -92f + (i == 1 ? 6f : 0f)), new Vector2(42f, 42f));
            stars[i].sprite = star;
        }
        gradeText = NewText(root, "Grade", font, "", 20f, Good, new Vector2(-232f, -130f), new Vector2(240f, 28f));
        gradeText.alpha = 0f;

        // ---- right: rows ----
        NewImage(root, "DetailCard", Card, new Vector2(132f, 6f), new Vector2(448f, 312f));
        NewRow(font, "題目分數", 132f, out questionValue, out questionBar, "80");
        NewRow(font, "語氣分數", 84f, out toneValue, out toneBar, "20");
        NewRow(font, "一次答對", 36f, out firstTryValue, out firstTryBar, "8 題");

        // ---- tiles: one per quiz and hands-on task ----
        NewText(root, "TilesLabel", font, "各題表現", 17f, Soft, new Vector2(-20f, -8f), new Vector2(120f, 24f)).alignment = TextAlignmentOptions.Left;
        NewText(root, "TilesLegend", font, "綠 一次答對　黃 答錯後答對　紅 未得分", 13f, Soft, new Vector2(208f, -8f), new Vector2(280f, 22f)).alignment = TextAlignmentOptions.Right;

        string[] ids = { "quiz_1", "quiz_2", "quiz_3", "quiz_4", "quiz_5", "quiz_6", "quiz_7", "quiz_8", "cuff", "arm" };
        float tile = 36f, gap = 5.5f;
        float firstX = 132f - (ids.Length * tile + (ids.Length - 1) * gap) * 0.5f + tile * 0.5f;
        for (int i = 0; i < ids.Length; i++)
        {
            PartScoreBoard.Item found = null;
            foreach (PartScoreBoard.Item item in items)
            {
                if (item != null && item.id == ids[i])
                    found = item;
            }
            Color color = found == null ? Track
                : found.solved && found.wrongAttempts == 0 ? Good
                : found.solved && found.points > 0 ? Fair
                : Bad;
            Image image = NewImage(root, "Tile_" + ids[i], color, new Vector2(firstX + i * (tile + gap), -42f), new Vector2(tile, tile));
            NewText(image.rectTransform, "Label", font, TileLabel(ids[i]), 19f, new Color(0.07f, 0.09f, 0.12f, 1f), Vector2.zero, new Vector2(tile, tile));
            image.gameObject.SetActive(false);
            tiles.Add(image.rectTransform);
        }

        commentText = NewText(root, "Comment", font, "", 17f, Color.white, new Vector2(132f, -104f), new Vector2(420f, 56f));
        commentText.enableAutoSizing = true;
        commentText.fontSizeMin = 11f;
        commentText.fontSizeMax = 17f;
        commentText.alpha = 0f;

        // ---- bottom ----
        uploadText = NewText(root, "Upload", font, "", 15f, Soft, new Vector2(-232f, -214f), new Vector2(256f, 44f));

        GameObject retry = new GameObject("Retry", typeof(RectTransform));
        retry.transform.SetParent(root, false);
        RectTransform retryRoot = (RectTransform)retry.transform;
        retryRoot.anchoredPosition = new Vector2(132f, -208f);
        retryRoot.sizeDelta = new Vector2(448f, 90f);
        retryGroup = retry.AddComponent<CanvasGroup>();
        retryGroup.alpha = 0f;
        retryImage = NewImage(retryRoot, "Button", Accent, new Vector2(-84f, 10f), new Vector2(250f, 56f));
        retryButton = retryImage.rectTransform;
        retryLabel = NewText(retryButton, "Label", font, retryText, 26f, new Color(0.05f, 0.08f, 0.12f, 1f), Vector2.zero, new Vector2(240f, 50f));
        quitImage = NewImage(retryRoot, "QuitButton", Track, new Vector2(138f, 10f), new Vector2(170f, 56f));
        quitButton = quitImage.rectTransform;
        quitLabel = NewText(quitButton, "Label", font, quitText, 26f, Color.white, Vector2.zero, new Vector2(160f, 50f));
        retryNote = NewText(retryRoot, "Note", font, retryNoteText, 14f, Soft, new Vector2(0f, -32f), new Vector2(440f, 22f));
        retry.SetActive(false);
    }

    private void NewRow(TMP_FontAsset font, string label, float y, out TMP_Text value, out Image bar, string max)
    {
        NewText(root, label + "_Label", font, label, 20f, Color.white, new Vector2(0f, y + 12f), new Vector2(160f, 28f)).alignment = TextAlignmentOptions.Left;
        value = NewText(root, label + "_Value", font, "0 / " + max, 22f, Color.white, new Vector2(264f, y + 12f), new Vector2(160f, 28f));
        value.alignment = TextAlignmentOptions.Right;
        NewImage(root, label + "_Track", Track, new Vector2(132f, y - 10f), new Vector2(424f, 8f));
        bar = NewImage(root, label + "_Bar", Accent, new Vector2(132f, y - 10f), new Vector2(424f, 8f));
        bar.sprite = WhiteSprite();
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
        bar.fillAmount = 0f;
    }

    private static TMP_FontAsset FindChineseFont()
    {
        // Same font as the prompt panels, so Chinese looks the same everywhere.
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
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    // ---- small pictures drawn in code (no image files needed) ----
    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    private static Sprite MakeRing()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float outer = size * 0.5f - 1f, inner = outer - size * 0.085f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float alpha = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite MakeStar()
    {
        const int size = 128, samples = 3;
        Vector2[] corners = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float radius = (i % 2 == 0 ? 0.48f : 0.21f) * size;
            corners[i] = new Vector2(size * 0.5f + Mathf.Cos(angle) * radius, size * 0.47f + Mathf.Sin(angle) * radius);
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                        bool hit = false;
                        for (int i = 0, j = corners.Length - 1; i < corners.Length; j = i++)
                        {
                            if ((corners[i].y > p.y) != (corners[j].y > p.y) &&
                                p.x < (corners[j].x - corners[i].x) * (p.y - corners[i].y) / (corners[j].y - corners[i].y) + corners[i].x)
                                hit = !hit;
                        }
                        if (hit)
                            inside++;
                    }
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
