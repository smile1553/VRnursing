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
        public bool playerActionRequired;
        public string playerPrompt;
        public string[] expectedKeywords;
        public bool allowLlmAssist = true;
        public string[] expectedIntents;
        public float minLlmConfidence = 0.7f;
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
        PopulateAsset(asset, data);

        return asset;
    }

    static void PopulateAsset(ScenarioAsset asset, ImportPayload data)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));
        if (data == null || data.steps == null)
            throw new ArgumentException("Invalid scenario payload", nameof(data));

        asset.steps = new List<ScenarioStep>(data.steps.Length);
        foreach (var importStep in data.steps)
            asset.steps.Add(ConvertStep(importStep));
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
            playerActionRequired = src.playerActionRequired,
            playerPrompt = src.playerPrompt,
            expectedKeywords = src.expectedKeywords,
            allowLlmAssist = src.allowLlmAssist,
            expectedIntents = src.expectedIntents,
            minLlmConfidence = Mathf.Clamp01(src.minLlmConfidence),
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

    [UnityEditor.MenuItem("VRNursing/Update Scenario Asset From JSON...")]
    static void UpdateExistingAssetFromJson()
    {
        var jsonPath = UnityEditor.EditorUtility.OpenFilePanel("Scenario JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(jsonPath)) return;

        var assetPath = UnityEditor.EditorUtility.OpenFilePanel("Scenario Asset", Application.dataPath, "asset");
        if (string.IsNullOrEmpty(assetPath)) return;

        string projectDataPath = Application.dataPath.Replace('\\', '/');
        string normalizedAssetPath = assetPath.Replace('\\', '/');
        if (!normalizedAssetPath.StartsWith(projectDataPath, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[ScenarioImporter] 請選擇專案 Assets 內的 ScenarioAsset。");
            return;
        }

        string unityAssetPath = "Assets" + normalizedAssetPath.Substring(projectDataPath.Length);
        var targetAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScenarioAsset>(unityAssetPath);
        if (targetAsset == null)
        {
            Debug.LogError("[ScenarioImporter] 選擇的檔案不是 ScenarioAsset。");
            return;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            var data = JsonUtility.FromJson<ImportPayload>(json);
            if (data == null || data.steps == null)
                throw new Exception("Invalid scenario json");

            PopulateAsset(targetAsset, data);
            UnityEditor.EditorUtility.SetDirty(targetAsset);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.EditorGUIUtility.PingObject(targetAsset);
            RuntimeLog.Info($"[ScenarioImporter] 已更新: {unityAssetPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScenarioImporter] 更新失敗: " + ex.Message);
        }
    }
#endif
}
