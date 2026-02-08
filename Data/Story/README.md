# ScenarioData 使用說明

- `gaga_vitals.json` 已依教授劇本拆解為多個步驟、考題與字幕，可直接透過 `VRNursing/Import Scenario JSON...` 選單匯入為 `ScenarioAsset`。
- 匯入後建議重新命名為 `GagaVitalsScenario.asset`，再綁定到 `ScenarioController` 測試。
- 若要修改流程，可直接編輯 JSON（保持欄位與 `ScenarioImporter` 定義一致），或匯入後再於 ScriptableObject 中微調。
- `cursorTargetId` 目前使用 `nurse`、`mother`、`child`、`system` 等文字，可在 `ScenarioCursorController` 內對應至實際角色。
