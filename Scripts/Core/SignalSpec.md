# Signal 規格（Story/Core）

## UI → Story
- `UI.Next`
  - payload: 空
  - 行為：呼叫 `ScenarioController.Next()`

- `UI.Choice`
  - payload: `{ choiceIndex: int }`
  - 行為：呼叫 `ScenarioController.SelectChoice(choiceIndex)`

## Input/VR
- `Input.Click` / `UI.Click`
  - payload: `{ targetId: string }`
  - targetId: `teddy_bear` / `stethoscope` / `thermometer` / `blood_pressure`
  - 行為：SignalBridge 轉送給 World，並預留 Story 行為判斷

## Story → World
- `story.step_started`
  - payload: `{ stepId }`
- `story.step_completed`
  - payload: `{ stepId }`
- `story.scenario_completed`
  - payload: `{}`
- `story.quiz_answered`
  - payload: `{ stepId, choiceIndex, correct }`
- `emotion_score`
  - payload: `{ stage, tension }`

## Story → World（指令類）
- `story.command`
  - payload: `{ stepId, type, payload }`
  - type: `PlayAnimation` / `PlayTimeline` / `PlayAudio` / `MoveCamera` / `TriggerVfx`
