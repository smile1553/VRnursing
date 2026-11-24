using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Every Skip (F8, controller B / Y, or clicking a Skip button) is written down, with a guess of WHY
// the student skipped:
//   網路卡住        the speech server did not answer (request hanging / failed)
//   沒收到語音      nothing was sent to the server (microphone problem, or the student said nothing)
//   有回答但沒過關  the server heard sentences, but none passed (maybe misheard: check the text)
//   送出語音但伺服器沒回  sound was sent but no answer came back (slow network)
// Log file: skip_log.csv in Application.persistentDataPath
//   PC:    C:\Users\<name>\AppData\LocalLow\<company>\<product>\skip_log.csv
//   Quest: Android/data/<package>/files/skip_log.csv
//
// Against misuse: while the server works, a Skip only works after the student has tried
// (Min Seconds Before Skip, and at least one sentence heard or Seconds Without Attempt passed).
// When the network is stuck it works at once. Tick Free Skip For Demo when recording a demo.
// Created on its own at start ("SKIP_AUDIT" in the Hierarchy while playing).
public class SkipAudit : MonoBehaviour
{
    [Header("Against misuse")]
    [Tooltip("While the speech server works, Skip only works after the student has tried.")]
    public bool blockEarlySkip = true;
    [Tooltip("Seconds the step must have been on before Skip works (when the network is fine).")]
    public float minSecondsBeforeSkip = 10f;
    [Tooltip("If the student has not said anything the server heard, Skip works only after this many seconds.")]
    public float secondsWithoutAttempt = 25f;
    [Tooltip("Tick when recording a demo video: Skip always works at once (still written down).")]
    public bool freeSkipForDemo = false;

    [Header("When is the network 'stuck'")]
    [Tooltip("A request to the server still unanswered after this many seconds = network stuck.")]
    public float stuckRequestSeconds = 5f;
    [Tooltip("More skips than this in one run is marked in the log.")]
    public int manySkips = 3;

    [Header("Skip buttons")]
    [Tooltip("A button counts as a Skip button when its object name contains this.")]
    public string skipNameContains = "skip";

    static SkipAudit instance;
    static string nextSource;

    AudioUploader uploader;
    float nextFind;
    int uploads, responses;
    int lastOutstanding;
    float pendingSince = -1f;
    float lastErrorTime = -1000f;
    string lastError = "";
    string lastRunId = "";
    readonly List<string> recentTexts = new List<string>();
    int skipCount;

    class Watch
    {
        public bool wasShown;
        public float shownAt;
        public int uploadsAt, responsesAt;
        public bool hooked;
        public bool disabledByUs;
        public string step;
    }
    readonly Dictionary<Button, Watch> watched = new Dictionary<Button, Watch>();
    float nextScan;

    GameObject toast;
    TextMeshProUGUI toastText;
    float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null || FindObjectOfType<SkipAudit>() != null)
            return;
        GameObject go = new GameObject("SKIP_AUDIT");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SkipAudit>();
    }

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    // Called by DemoRecordingMode before it presses a Skip button from F8 / the controller.
    // Returns false (and tells the student why) when the skip is not allowed yet.
    public static bool TryKeySkip(Button button, string source)
    {
        if (instance == null)
            return true;
        if (!instance.Allowed(button, out string why))
        {
            instance.ShowToast(why);
            instance.Write(button, source, "被擋下：" + why);
            return false;
        }
        nextSource = source;
        return true;
    }

    void Update()
    {
        TrackAudio();

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            if (!string.IsNullOrEmpty(skipNameContains))
                foreach (Button button in FindObjectsOfType<Button>(true))
                    if (button != null && !watched.ContainsKey(button) && button.name.IndexOf(skipNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                        watched[button] = new Watch();
        }

        foreach (KeyValuePair<Button, Watch> pair in watched)
        {
            Button button = pair.Key;
            Watch w = pair.Value;
            if (button == null)
                continue;
            if (!w.hooked)
            {
                w.hooked = true;
                Button b = button;
                button.onClick.AddListener(() => OnSkipClicked(b));
            }
            bool shown = button.isActiveAndEnabled;
            if (shown && !w.wasShown)
            {
                w.shownAt = Time.unscaledTime;
                w.uploadsAt = uploads;
                w.responsesAt = responses;
                w.step = ScenarioProgress.Current;
            }
            w.wasShown = shown;

            // Clicking the visible button: greyed out until it is allowed.
            if (shown)
            {
                bool ok = Allowed(button, out _);
                if (!ok && button.interactable)
                {
                    button.interactable = false;
                    w.disabledByUs = true;
                }
                else if (ok && w.disabledByUs)
                {
                    button.interactable = true;
                    w.disabledByUs = false;
                }
            }
        }

        if (toast != null && toast.activeSelf)
        {
            if (Time.unscaledTime > toastUntil)
                toast.SetActive(false);
            else
                PlaceToast();
        }
    }

    void TrackAudio()
    {
        if (uploader == null && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 1f;
            uploader = FindObjectOfType<AudioUploader>();
            if (uploader != null)
                uploader.AudioResponseAccepted += OnResponse;
        }
        if (uploader == null)
            return;

        int outstanding = uploader.OutstandingRequestCount;
        if (outstanding > lastOutstanding)
            uploads += outstanding - lastOutstanding;
        if (outstanding > 0 && pendingSince < 0f)
            pendingSince = Time.unscaledTime;
        else if (outstanding == 0)
            pendingSince = -1f;
        lastOutstanding = outstanding;

        string error = uploader.LastUploadError ?? "";
        if (error.Length > 0 && error != lastError)
            lastErrorTime = Time.unscaledTime;
        lastError = error;
    }

    void OnDestroy()
    {
        if (uploader != null)
            uploader.AudioResponseAccepted -= OnResponse;
    }

    void OnResponse(AudioAnalysisResponse response, string json)
    {
        responses++;
        if (response == null)
            return;
        if (!string.IsNullOrEmpty(response.studentRunId))
            lastRunId = response.studentRunId;
        if (!string.IsNullOrEmpty(response.text))
        {
            recentTexts.Add(response.text.Trim());
            if (recentTexts.Count > 3)
                recentTexts.RemoveAt(0);
        }
    }

    bool NetworkStuck(Watch w, out string why)
    {
        why = "";
        if (uploader == null)
        {
            why = "找不到語音系統";
            return true;
        }
        if (uploader.HasBlockingAudioFailure)
        {
            why = "語音上傳失敗";
            return true;
        }
        if (pendingSince >= 0f && Time.unscaledTime - pendingSince > stuckRequestSeconds)
        {
            why = $"伺服器 {Time.unscaledTime - pendingSince:0} 秒沒回應";
            return true;
        }
        if (w != null && lastErrorTime >= w.shownAt && lastError.Length > 0)
        {
            why = "上傳出錯";
            return true;
        }
        return false;
    }

    bool Allowed(Button button, out string why)
    {
        why = "";
        if (freeSkipForDemo || !blockEarlySkip)
            return true;
        watched.TryGetValue(button, out Watch w);
        if (w == null || !w.wasShown)
            return true;
        if (NetworkStuck(w, out _))
            return true;
        float on = Time.unscaledTime - w.shownAt;
        bool tried = responses > w.responsesAt;
        if (on < minSecondsBeforeSkip)
        {
            why = $"請先試著回答，{Mathf.CeilToInt(minSecondsBeforeSkip - on)} 秒後才能跳過";
            return false;
        }
        if (!tried && on < secondsWithoutAttempt)
        {
            why = $"系統還沒聽到你的回答，請再說一次（{Mathf.CeilToInt(secondsWithoutAttempt - on)} 秒後才能跳過）";
            return false;
        }
        return true;
    }

    void OnSkipClicked(Button button)
    {
        string source = string.IsNullOrEmpty(nextSource) ? "點按鈕" : nextSource;
        nextSource = null;
        skipCount++;
        watched.TryGetValue(button, out Watch w);
        string verdict;
        if (freeSkipForDemo)
            verdict = "示範模式";
        else if (NetworkStuck(w, out string net))
            verdict = "網路卡住（" + net + "）";
        else if (w != null && uploads == w.uploadsAt && responses == w.responsesAt)
            verdict = "沒收到語音（麥克風問題或沒開口）";
        else if (w != null && responses > w.responsesAt)
            verdict = "有回答但沒過關（看下面的句子是否被聽錯）";
        else if (w != null && uploads > w.uploadsAt)
            verdict = "送出語音但伺服器沒回（可能網路慢）";
        else
            verdict = "太快跳過";
        if (skipCount > manySkips)
            verdict += $"；本次已跳過 {skipCount} 次";
        Write(button, source, verdict);
    }

    void Write(Button button, string source, string verdict)
    {
        watched.TryGetValue(button, out Watch w);
        float on = w != null ? Time.unscaledTime - w.shownAt : -1f;
        int heard = w != null ? responses - w.responsesAt : -1;
        int sent = w != null ? uploads - w.uploadsAt : -1;
        string step = w != null && !string.IsNullOrEmpty(w.step) ? w.step : ScenarioProgress.Current;
        string texts = string.Join(" / ", recentTexts);
        float pending = pendingSince >= 0f ? Time.unscaledTime - pendingSince : 0f;

        Debug.Log($"[SkipAudit] {verdict} | step={step} source={source} shown={on:0}s sent={sent} heard={heard} pending={pending:0}s lastError='{lastError}' texts='{texts}'");

        try
        {
            string path = Path.Combine(Application.persistentDataPath, "skip_log.csv");
            bool isNew = !File.Exists(path);
            StringBuilder line = new StringBuilder();
            if (isNew)
                line.Append("\uFEFF時間,學生run,步驟,方式,按鈕出現幾秒,送出語音段數,伺服器聽到句數,等待回應秒數,最後錯誤,最近聽到的句子,判斷\n");
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
                .Append(Csv(lastRunId)).Append(',')
                .Append(Csv(step)).Append(',')
                .Append(Csv(source)).Append(',')
                .Append(on.ToString("0")).Append(',')
                .Append(sent).Append(',')
                .Append(heard).Append(',')
                .Append(pending.ToString("0")).Append(',')
                .Append(Csv(lastError)).Append(',')
                .Append(Csv(texts)).Append(',')
                .Append(Csv(verdict)).Append('\n');
            File.AppendAllText(path, line.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SkipAudit] Could not write skip_log.csv: " + e.Message);
        }
    }

    static string Csv(string value)
    {
        value = value ?? "";
        return "\"" + value.Replace("\"", "\"\"").Replace("\n", " ") + "\"";
    }

    // A short message in front of the eyes (VR and PC).
    void ShowToast(string message)
    {
        if (toast == null)
            BuildToast();
        if (toastText != null)
            toastText.text = message;
        toast.SetActive(true);
        toastUntil = Time.unscaledTime + 2.5f;
        PlaceToast();
    }

    void BuildToast()
    {
        toast = new GameObject("SkipAudit_Toast", typeof(RectTransform));
        toast.transform.SetParent(transform, false);
        Canvas canvas = toast.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 70;
        RectTransform root = (RectTransform)toast.transform;
        root.sizeDelta = new Vector2(900f, 140f);
        root.localScale = Vector3.one * 0.0007f;

        GameObject bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(root, false);
        Image image = bg.AddComponent<Image>();
        image.color = new Color(0.10f, 0.13f, 0.20f, 0.92f);
        image.raycastTarget = false;
        RectTransform bgRect = image.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(root, false);
        toastText = textGo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = FindChineseFont();
        if (font != null)
            toastText.font = font;
        toastText.fontSize = 40f;
        toastText.color = Color.white;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.raycastTarget = false;
        RectTransform textRect = toastText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);
        toast.SetActive(false);
    }

    void PlaceToast()
    {
        Camera cam = Camera.main;
        if (cam == null || toast == null)
            return;
        Transform head = cam.transform;
        float distance = Mathf.Max(0.3f, HudDepth.Distance - 0.05f);
        toast.transform.position = head.position + head.forward * distance - head.up * 0.12f;
        toast.transform.rotation = Quaternion.LookRotation(toast.transform.position - head.position, head.up);
    }

    static TMP_FontAsset FindChineseFont()
    {
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
}
