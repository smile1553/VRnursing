Core 目錄說明

- GameBootstrapper.cs
  掛在場景中的 GameManager 物件上，用來初始化核心元件並在需要時自動啟動 Scenario。
  建議：GameObject 名稱為 GameManager。

- SignalBus.cs
  提供簡單事件匯流排，方便將情緒與劇情事件集中轉發給其他模組。

使用方式（最小）：
1) 在場景建立空物件命名為 GameManager。
2) 掛上 GameBootstrapper (可選：也掛 SignalBus)。
3) 在 Inspector 指定 defaultScenario（例如 gaga_vitals_Scenario.asset）。
