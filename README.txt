Core：系統骨架與串接

負責把各系統啟動、接起來，提供共用的訊號溝通。

GameBootstrapper.cs
專案入口。負責在場景開始時初始化系統、建立或找到必要的管理物件與引用。

SignalBus.cs
全專案的訊號中心。提供送出/接收 signal 的方式，讓 Story、Input、World、UI 彼此溝通。

Story：劇情怎麼跑

負責劇情流程、等待互動、分支判斷、狀態管理。只管「邏輯」，不管顯示與動畫。

StoryRunner.cs
執行劇本流程。讀取 story 資料、依序跑節點、等待 signal、觸發分支與推進下一步。

StoryData.cs
劇本資料容器。代表整份劇本（節點列表、起點、全局設定等）。

StoryNode.cs
單一節點的資料定義。包含台詞、選項、等待條件、要觸發的 action、下一節點等欄位。

StoryVariables.cs
劇情變數與狀態。保存目前進度、旗標、數值（例如情緒分數、是否完成某動作）。

ConditionEvaluator.cs
分支條件判斷器。負責把「條件字串/規則」用 StoryVariables 的值算出 true/false。

UI：畫面怎麼顯示

負責對話框、選項按鈕、提示文字的顯示與點擊回傳。只管「顯示與回傳」，不管劇情順序。

DialogueUI.cs
顯示角色名與台詞文字，控制對話框開關與「下一句」提示。

ChoiceButton.cs
單顆選項按鈕元件。設定文字、被點擊時回傳選項 index 給系統。

HintUI.cs
顯示互動提示（例如請抓玩具、請走到床邊），控制提示文字顯示/隱藏。

VR：VR 互動技術層

負責 VR 裝置或互動觸發的具體實作，把玩家在 VR 中的操作轉成可用的訊號。

VRInputRouter.cs
統一入口。把不同來源（VR 控制器、模擬輸入）整理成同一套 signal 丟出去。

VRInteractTrigger.cs
互動觸發器。負責按、抓、碰等互動事件的偵測與發送 signal。

VRZoneTrigger.cs
區域觸發器。玩家進入/離開指定區域時送出 signal（例如走到床邊）。

VRPlayerRig.cs
玩家 Rig 管理。負責玩家頭/手的引用與基礎設定，提供 VR 相關的共用存取點。

Input：玩家行為層

負責把玩家操作整理成「右手在做什麼」這種行為訊號。可用滑鼠模擬右手，也可接 VR。

PlayerInputRoot.cs
玩家輸入總控。管理玩家是否可互動、目前模式（測試/正式）、集中輸入狀態。

RightHandInput.cs
右手輸入控制。處理右手的點擊/抓取等操作，產生對應的互動事件。

InteractRaycaster.cs
互動射線偵測。從相機或右手射出 ray，找出玩家指到/點到的物件，交給互動系統處理。

World：世界怎麼回應

負責接收 Story 的指令與情緒結果，讓世界與角色做出實際反應（角色行為、動畫、音效）。

WorldDirector.cs
世界總控。接收系統事件，決定要呼叫哪些世界行為（例如叫 kid 反應、叫 mom 安撫）。

AudioController.cs
音效管理。集中播放/停止世界音效或角色相關音效，避免各處亂播難以控管。

World/Actors：角色共用層

放所有角色共用的基底、註冊與查找。

ActorRoot.cs
角色基底元件。定義角色 id（mom/kid）、共用引用（Animator/AudioSource）或共用狀態。

ActorRegistry.cs
角色註冊表。負責把場景中的角色收集起來，提供用 id 取得 mom/kid 物件的功能。

World/Actors/Kid：kid 專屬

KidRoot.cs
kid 角色本體。kid 的 id/引用設定與初始化入口，讓系統能穩定辨識此物件是 kid。

KidEmotionResponder.cs
kid 情緒反應器。根據情緒分數區間決定 kid 要呈現的反應（冷靜、不安、哭、崩潰）。

World/Actors/Mom：mom 專屬

MomRoot.cs
mom 角色本體。mom 的 id/引用設定與初始化入口，讓系統能穩定辨識此物件是 mom。

MomActionResponder.cs
mom 行為執行器。接收指令並執行 mom 的動作（靠近、安撫、說話），之後可接動畫與音效。

AI：情緒系統

負責產生情緒結果（分數、類型），提供給 Story 與 World 使用。只算結果，不負責演出。

EmotionService.cs
情緒服務入口。統一對外提供目前情緒分數/狀態，並在更新時通知系統。

EmotionTypes.cs
情緒資料型別定義。包含情緒 enum、結果結構（分數、標籤、信心值等）。

EmotionSmoother.cs
平滑與防抖處理。把原始情緒輸出變得穩定，避免分數跳動造成角色一直切狀態。

Debug：測試工具

只用來加速測試，不影響正式流程。讓你不用一直從頭跑劇情也能檢查互動與反應。

EmotionDebugPanel.cs
情緒測試面板。手動調整情緒分數，快速測 kid 的反應是否正確。

StoryDebugPanel.cs
劇情測試面板。提供考虑下一句、跳段落、快轉等功能以便測分支。

SignalMonitor.cs
訊號監看器。列出目前收到/送出的 signal，確認 Input/VR 是否有成功觸發。

Data/Story：劇本資料

放非程式資料，目前採用單一章節整合檔。

story_main.json
單一整合章節劇本。包含全部流程節點、台詞、選項、等待互動與 action 定義。