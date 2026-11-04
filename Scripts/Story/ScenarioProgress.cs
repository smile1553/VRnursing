using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

// Which part of the story the student is in, and how long each part took.
//
//   - Every sentence sent to the Backend is labelled with the current part (it used to be
//     "intro_nurse" for the whole story), so the report can say where each sentence was said.
//   - The time spent in each part is sent with the result, for "各步驟花費時間" in the report.
//
// Parts: "login" (nurses' station) -> "part0" (at the ward door) -> "part1" ... "part9".
// Part 0 and 1 are announced by the door flow; parts 2-9 are picked up from the line each
// story script already writes when it starts ("[Part5] StartPart5: ..."), so none of those
// scripts had to change.
public static class ScenarioProgress
{
    [Serializable]
    public class StepTime
    {
        public string id;
        public float seconds;
    }

    static readonly List<StepTime> finished = new List<StepTime>();
    static readonly Regex startLine = new Regex(@"^\[Part([2-9])\] StartPart\1\b", RegexOptions.Compiled);
    static string current = "login";
    static float currentSince;
    static bool hooked;

    public static string Current => current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!hooked)
        {
            hooked = true;
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (mode == LoadSceneMode.Single)
                    Restart();
            };
        }
        Restart();
    }

    // A new practice starts (scene loaded or reloaded).
    public static void Restart()
    {
        finished.Clear();
        current = "login";
        currentSince = Time.realtimeSinceStartup;
    }

    public static void Enter(string id)
    {
        if (string.IsNullOrEmpty(id) || id == current)
            return;
        finished.Add(new StepTime { id = current, seconds = Time.realtimeSinceStartup - currentSince });
        current = id;
        currentSince = Time.realtimeSinceStartup;
    }

    // Time spent in every part so far, the current one included.
    public static StepTime[] Snapshot()
    {
        List<StepTime> all = new List<StepTime>(finished);
        all.Add(new StepTime { id = current, seconds = Time.realtimeSinceStartup - currentSince });
        return all.ToArray();
    }

    static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Log || string.IsNullOrEmpty(message) || message.Length < 18 || message[0] != '[')
            return;
        Match match = startLine.Match(message);
        if (match.Success)
            Enter("part" + match.Groups[1].Value);
    }
}
