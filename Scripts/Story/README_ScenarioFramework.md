# VRNursing Scenario Framework 摘要

這個資料夾目前包含完整的劇本與情緒框架，尚未綁定實際 Unity 場景即可先撰寫、模擬及記錄流程。

## 主要元件

- `ScenarioAsset`：ScriptableObject 劇本，每一個 `ScenarioStep` 支援台詞、字幕、考題、情緒門檻與指令。
- `ScenarioStep.playerActionRequired`/`playerPrompt`：可設定玩家需要完成的語音或 XR 行為提示，讓學生自行判斷要說什麼、做什麼；未來可搭配語音辨識或互動檢查。
- `ScenarioController`：讀取 ScenarioAsset，推進步驟、顯示 UI，並對外發出 `cursorTargetChanged`、`stepStarted`、`stepCompleted`、`quizAnswered` 等事件。
- `ScenarioCursorController`、`ScenarioDialogueBubble`、`ScenarioQuizSummary`：訂閱上述事件，讓游標、對話泡泡、答題結果 UI 依劇情自動更新。
- `ScenarioLogger`：將每個步驟與考題加上情緒快照記錄成 JSON，方便後續評估。
- `EmotionStateManager`：集中管理 tension/stage；可由 RunAI 或 `EmotionStateSimulator` 手動驅動。
- `EmotionHud`：顯示張力/段位/意圖。
- `EmotionStateSimulator` + `EmotionSimClip`：讓整個流程可離線播放情緒曲線或手動指定張力值。
- `ScenarioDebugConsole`：簡易 OnGUI 面板，可跳步驟、控制流程、觀察目前情緒。
- `ScenarioCommandExecutor` + `Animation/Audio/Timeline/CameraCommandTarget`：依 `ScenarioStep.commands` 執行動畫、音訊、TimeLine 或鏡頭切換，可先用命令名稱對應 placeholder，待實際動畫完成再替換。

## 劇本匯入

`ScenarioImporter` 讀取 JSON 並建立 `ScenarioAsset`。在 Unity Editor 會額外提供 `VRNursing/Import Scenario JSON...` 選單：
1. 準備 JSON（欄位對應 `ScenarioImporter` 的 `ImportStep` 結構）。
2. 選單載入後會在 Project 視窗產生對應的 ScriptableObject。

## 建議使用流程

1. **撰寫劇本**：用 JSON 或直接在 Inspector 編輯 `ScenarioAsset`。
2. **模擬情緒**：啟動 `EmotionStateSimulator` 播放曲線，觀測 `EmotionHud` 與 `ScenarioController` 是否依 gate 進出分支。
3. **測試互動**：開啟 `ScenarioDebugConsole`，逐步檢查考題、字幕、游標/泡泡是否正確。
4. **記錄與驗證**：掛上 `ScenarioLogger`，跑完整流程後檢查輸出的 JSON（位於 `persistentDataPath/ScenarioLogs`）。
5. **整合實際場景**：待角色動畫與 UI 完成後，將這些腳本拖入對應物件、綁定 UI 元件，並在 `ScenarioCommandExecutor` 的各 target 上定義命令→動畫/音效/鏡頭的對應，即可復用同一份劇本資料。

以上腳本均為純 C# / MonoBehaviour，尚未綁定具體模型，方便先行開發邏輯。待真實資產到位再替換 UI、動畫即可。
