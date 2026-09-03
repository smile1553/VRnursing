using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ScenarioImporter
{
    [Serializable]
    class ImportPayload
    {
        public ImportStep[] steps;
    }

    [Serializable]
    class ImportStep
    {
        public string id;
        public string speaker;
        public string dialogue;
        public string cursorTargetId;
        public float autoAdvanceDelay;
        public bool waitForClick = true;
        public ImportSubtitle subtitle;
        public ImportQuiz quiz;
        public ImportEmotionGate emotionGate;
        public ImportCommand[] commands;
        public int explicitNextIndex = -1;
    }

    [Serializable]
    class ImportSubtitle
    {
        public string text;
        public float duration = 3f;
    }

    [Serializable]
    class ImportQuiz
    {
        public string question;
        public string explanation;
        public string[] options;
        public int correctIndex;
        public bool requireCorrectToProceed = true;
    }

    [Serializable]
    class ImportEmotionGate
    {
        public bool blockWhenAnxious;
        public int anxiousStageThreshold = 1;
        public int fallbackStepIndex = -1;
        public int calmStageRequirement = 0;
        public string blockedSubtitle;
    }

    [Serializable]
    class ImportCommand
    {
        public string type;
        public string payload;
    }

    public static ScenarioAsset FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("json is empty");
        var data = JsonUtility.FromJson<ImportPayload>(json);
        if (data == null || data.steps == null)
            throw new Exception("Invalid scenario json");

        var asset = ScriptableObject.CreateInstance<ScenarioAsset>();
        asset.steps = new List<ScenarioStep>(data.steps.Length);
        foreach (var importStep in data.steps)
            asset.steps.Add(ConvertStep(importStep));

        return asset;
    }

    static ScenarioStep ConvertStep(ImportStep src)
    {
        var step = new ScenarioStep
        {
            id = src.id,
            speaker = ParseSpeaker(src.speaker),
            dialogue = src.dialogue,
            cursorTargetId = src.cursorTargetId,
            autoAdvanceDelay = src.autoAdvanceDelay,
            waitForClick = src.waitForClick,
            explicitNextIndex = src.explicitNextIndex
        };
        if (src.subtitle != null && !string.IsNullOrEmpty(src.subtitle.text))
            step.subtitle = new ScenarioSubtitle { text = src.subtitle.text, duration = src.subtitle.duration };
        if (src.quiz != null && !string.IsNullOrEmpty(src.quiz.question))
            step.quiz = new ScenarioQuiz
            {
                question = src.quiz.question,
                explanation = src.quiz.explanation,
                options = src.quiz.options,
                correctIndex = src.quiz.correctIndex,
                requireCorrectToProceed = src.quiz.requireCorrectToProceed
            };
        if (src.emotionGate != null)
            step.emotionGate = new ScenarioEmotionGate
            {
                blockWhenAnxious = src.emotionGate.blockWhenAnxious,
                anxiousStageThreshold = src.emotionGate.anxiousStageThreshold,
                fallbackStepIndex = src.emotionGate.fallbackStepIndex,
                calmStageRequirement = src.emotionGate.calmStageRequirement,
                blockedSubtitle = src.emotionGate.blockedSubtitle
            };
        if (src.commands != null && src.commands.Length > 0)
        {
            var cmds = new List<ScenarioCommand>(src.commands.Length);
            foreach (var cmd in src.commands)
            {
                if (cmd == null) continue;
                if (!Enum.TryParse(cmd.type, true, out ScenarioCommandType type))
                    type = ScenarioCommandType.None;
                cmds.Add(new ScenarioCommand { type = type, payload = cmd.payload });
            }
            step.commands = cmds.ToArray();
        }
        return step;
    }

    static ScenarioSpeaker ParseSpeaker(string s)
    {
        if (string.IsNullOrEmpty(s))
            return ScenarioSpeaker.Narrator;
        if (Enum.TryParse(s, true, out ScenarioSpeaker speaker))
            return speaker;
        return ScenarioSpeaker.Narrator;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("VRNursing/Import Scenario JSON...")]
    static void ImportFromFile()
    {
        var path = UnityEditor.EditorUtility.OpenFilePanel("Scenario JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var asset = FromJson(json);
            string targetPath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save Scenario Asset", Path.GetFileNameWithoutExtension(path) + "_Scenario", "asset", "選擇要存放 ScenarioAsset 的位置");
            if (!string.IsNullOrEmpty(targetPath))
            {
                UnityEditor.AssetDatabase.CreateAsset(asset, targetPath);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.EditorUtility.FocusProjectWindow();
                UnityEditor.Selection.activeObject = asset;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScenarioImporter] 解析失敗: " + ex.Message);
        }
    }
#endif
}
