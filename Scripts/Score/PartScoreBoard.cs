using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Scoring for the part-by-part story (Part 1-9), which does not go through ScenarioController.
//
//   * Quizzes 1-8: watched automatically through the shared "Correct_Popup" / "Wrong_Popup"
//     of the quiz canvas. Every wrong answer is counted.
//   * Hands-on tasks (choosing the cuff, choosing where its arrow goes): reported by the
//     story scripts with ReportAttempt().
//   * When the story ends, Complete() writes the detailed score to a file and tells the
//     existing uploader that the scenario is finished, so the result goes to the backend.
//
// What goes to the backend (its current format): the number of quizzes answered right at the
// first try (0-8) and the tone score (0-20). The detailed score below is kept in the report
// file and in the Console.
public class PartScoreBoard : MonoBehaviour
{
    [Header("Detailed score (out of 80; the tone score adds 20)")]
    [Tooltip("Points for each of the 8 quizzes.")]
    public int quizPoints = 8;
    [Tooltip("Points for each hands-on task (cuff, arrow position).")]
    public int taskPoints = 8;
    [Tooltip("Points lost for every wrong answer / wrong pick (never below 0 for that item).")]
    public int wrongPenalty = 4;
    [Tooltip("Show the end screen (score counting up, try again) when the story is finished.")]
    public bool showEndScreen = true;

    [Header("Watching the quizzes")]
    public string correctPopupName = "Correct_Popup";
    public string wrongPopupName = "Wrong_Popup";
    public string quizPanelPrefix = "Quiz_Panel_";

    [Serializable]
    public class Item
    {
        public string id;
        public bool isQuiz;
        public int wrongAttempts;
        public bool solved;
        public int points;
    }

    [Serializable]
    private class Report
    {
        public string generatedAt;
        public int quizzesRightFirstTry;
        public int detailedScore;
        public int detailedMax;
        public int toneScore;
        public int detailedTotal;
        public Item[] items;
    }

    private static PartScoreBoard instance;
    private readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
    private readonly List<string> order = new List<string>();
    private GameObject correctPopup, wrongPopup;
    private bool correctWasShown, wrongWasShown;
    private float nextScan;
    private bool completed;

    public static readonly string[] TaskIds = { "cuff", "arm" };

    // Detailed question score (0-80) of the story that just finished; -1 until it finishes.
    // The uploader sends it to the Backend as "questionScore".
    public static int LastDetailedScore { get; private set; } = -1;
    // The rest of that result, for the end screen.
    public static int LastToneScore { get; private set; }
    public static int LastRightFirstTry { get; private set; }
    public static Item[] LastItems { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    private static PartScoreBoard Ensure()
    {
        if (instance == null)
            instance = FindObjectOfType<PartScoreBoard>(true);
        if (instance == null)
            instance = new GameObject("PartScoreBoard (auto)").AddComponent<PartScoreBoard>();
        return instance;
    }

    // New student: start from zero.
    public static void ResetBoard()
    {
        LastDetailedScore = -1;
        if (instance == null)
            return;
        instance.items.Clear();
        instance.order.Clear();
        instance.completed = false;
    }

    // A hands-on task (or a quiz reported by hand): one attempt, right or wrong.
    public static void ReportAttempt(string id, bool correct)
    {
        Ensure().Record(id, false, correct);
    }

    // End of the story: write the report and send the result.
    public static void Complete()
    {
        Ensure().Finish();
    }

    private Item Get(string id, bool isQuiz)
    {
        if (!items.TryGetValue(id, out Item item))
        {
            item = new Item { id = id, isQuiz = isQuiz };
            items[id] = item;
            order.Add(id);
        }
        return item;
    }

    private void Record(string id, bool isQuiz, bool correct)
    {
        Item item = Get(id, isQuiz);
        if (item.solved)
            return;

        bool firstAttempt = item.wrongAttempts == 0;
        if (correct)
            item.solved = true;
        else
            item.wrongAttempts++;

        PerformanceScoreManager manager = FindObjectOfType<PerformanceScoreManager>();
        if (manager != null)
        {
            if (isQuiz && (firstAttempt || correct))
                manager.ReportExternalQuiz(id, correct && firstAttempt);   // counts only once per quiz
            if (!correct)
                manager.ReportExternalEvent(isQuiz ? "quiz_wrong_attempt" : "task_wrong_attempt", id, wrongPenalty, "wrong x" + item.wrongAttempts);
            else if (!isQuiz)
                manager.ReportExternalEvent("task", id, 0, item.wrongAttempts == 0 ? "right first try" : "right after " + item.wrongAttempts + " wrong");
        }
        Debug.Log($"[PartScoreBoard] {id}: {(correct ? "right" : "wrong")} (wrong so far: {item.wrongAttempts}).", this);
    }

    private void Update()
    {
        // The eight quizzes share one "correct" and one "wrong" pop-up: when one of them
        // appears, the quiz that is open at that moment was answered right / wrong.
        if ((correctPopup == null || wrongPopup == null) && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid())
                    continue;
                if (correctPopup == null && t.name == correctPopupName) correctPopup = t.gameObject;
                else if (wrongPopup == null && t.name == wrongPopupName) wrongPopup = t.gameObject;
            }
        }

        bool correctShown = correctPopup != null && correctPopup.activeInHierarchy;
        bool wrongShown = wrongPopup != null && wrongPopup.activeInHierarchy;
        if (correctShown && !correctWasShown)
            RecordOpenQuiz(true);
        if (wrongShown && !wrongWasShown)
            RecordOpenQuiz(false);
        correctWasShown = correctShown;
        wrongWasShown = wrongShown;
    }

    private void RecordOpenQuiz(bool correct)
    {
        GameObject popup = correct ? correctPopup : wrongPopup;
        Transform root = popup.transform.root;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
        {
            if (t.name.StartsWith(quizPanelPrefix) && t.gameObject.activeInHierarchy)
            {
                Record("quiz_" + t.name.Substring(quizPanelPrefix.Length), true, correct);
                return;
            }
        }
        Debug.LogWarning("[PartScoreBoard] A quiz answer was shown but no open quiz panel was found.", this);
    }

    private void Finish()
    {
        if (completed)
            return;
        completed = true;

        int rightFirstTry = 0;
        int detailed = 0;
        int max = 8 * quizPoints + TaskIds.Length * taskPoints;
        List<Item> list = new List<Item>();
        foreach (string id in order)
        {
            Item item = items[id];
            int full = item.isQuiz ? quizPoints : taskPoints;
            item.points = item.solved ? Mathf.Max(0, full - item.wrongAttempts * wrongPenalty) : 0;
            detailed += item.points;
            if (item.isQuiz && item.solved && item.wrongAttempts == 0)
                rightFirstTry++;
            list.Add(item);
        }

        LastDetailedScore = Mathf.Clamp(detailed, 0, 80);

        PerformanceScoreManager manager = FindObjectOfType<PerformanceScoreManager>();
        int tone = manager != null ? manager.ToneScore : 0;
        LastToneScore = tone;
        LastRightFirstTry = rightFirstTry;
        LastItems = list.ToArray();

        Report report = new Report
        {
            generatedAt = DateTime.Now.ToString("o"),
            quizzesRightFirstTry = rightFirstTry,
            detailedScore = detailed,
            detailedMax = max,
            toneScore = tone,
            detailedTotal = detailed + tone,
            items = list.ToArray()
        };

        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "ScenarioLogs");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "part_score_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("[PartScoreBoard] Detailed score saved -> " + path, this);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PartScoreBoard] Could not save the detailed score: " + e.Message, this);
        }

        Debug.Log($"[PartScoreBoard] FINISHED. Quizzes right at first try: {rightFirstTry}/8. " +
                  $"Detailed score: {detailed}/{max} + tone {tone}/20 = {detailed + tone}/100. " +
                  $"Sent to the backend: correctCount={(manager != null ? manager.QuizCorrectCount : rightFirstTry)}, toneScore={tone}, questionScore={LastDetailedScore}.", this);

        // The existing pipeline (report file + upload to the backend) starts from this event.
        ScenarioController controller = FindObjectOfType<ScenarioController>(true);
        if (controller != null && controller.onScenarioCompleted != null)
            controller.onScenarioCompleted.Invoke();
        else
            Debug.LogWarning("[PartScoreBoard] ScenarioController not found: the result was not sent to the backend.", this);

        // End screen: the score counts up, then "try again".
        if (showEndScreen)
            ScenarioEndScreen.Show();
    }
}
