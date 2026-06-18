using System;
using UnityEngine;

[Serializable]
public class FusionResultDto {
    public float tension;      // 可能有
    public string emotion;     // 後備用
}

public class RunAI : MonoBehaviour
{
    public enum Mode { Absolute, Accumulate }
    [Header("運作模式")]
    public Mode mode = Mode.Accumulate;

    [Header("除錯")]
    public bool logStageChanges = false;

    [Header("Animator 綁定")]
    public Animator animator;
    public string stageParam = "EmotionStage"; // Animator 裡的 Int 參數

    [Header("Stage 數值（對 Animator）")]
    public int minStage = -2;   // 最低段位
    public int maxStage =  2;   // 最高段位

    [Header("Absolute 模式用的門檻")]
    public float cryThreshold1   = 1f;
    public float cryThreshold2   = 2f;
    public float relaxThreshold1 = -1f;
    public float relaxThreshold2 = -2f;

    [Header("Accumulate 模式參數")]
    public float gain = 1.0f;            // 把輸入轉成增量的倍率（tension * gain）
    public float deadzone = 0.15f;       // 絕對值小於這個就當 0（抗雜訊）
    public float maxAbsDeltaPerTick = 1; // 每次更新最大增量（防瞬間暴衝）
    public float decayPerSecond = 0.50f; // 自然回歸 0 的速度（每秒衰減多少「累積值」）
    public float snapHysteresis = 0.25f; // 轉換成整數段位的黏滯門檻，避免抖動

    [Header("防抖（對輸出段位）")]
    public int requiredConsecutive = 1;

    public event Action<int> StageChanged;

    // ===== 內部狀態 =====
    private float _acc;          // 累積用的連續值（Accumulate 模式下）
    private int   _lastStage;
    private int   _pendingStage;
    private int   _pendingCount;

    public int CurrentStage => _lastStage;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        SetStageImmediate(0);
    }

    // 被網路層呼叫
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        FusionResultDto data = null;
        try { data = JsonUtility.FromJson<FusionResultDto>(json); }
        catch { /* 忍一下 */ }

        float input = 0f;

        if (data != null)
        {
            // 1) 盡量用 tension；若無，就用 emotion 做個簡單映射
            if (!float.IsNaN(data.tension) && (data.tension != 0f || json.Contains("\"tension\"")))
            {
                input = data.tension; // e.g. -2~+2 或其他
            }
            else if (!string.IsNullOrEmpty(data.emotion))
            {
                switch (data.emotion.ToLowerInvariant())
                {
                    case "angry": case "fear": case "surprise": case "stress": case "cry":
                        input = +1.2f; break; // 偏向上升
                    case "happy": case "joy": case "positive":
                        input = +0.7f; break;
                    case "sad": case "tired": case "relax":
                        input = -0.9f; break; // 偏向下降
                    default:
                        input = 0f; break;
                }
            }
        }

        // 根據模式更新
        if (mode == Mode.Absolute)
        {
            int stage = MapAbsoluteToStage(input);
            TrySetStageWithDebounce(stage);
        }
        else
        {
            int stage = UpdateAccumulateAndMapToStage(input);
            TrySetStageWithDebounce(stage);
        }
    }

    // === Absolute：當下值直接對表 ===
    private int MapAbsoluteToStage(float t)
    {
        if      (t >= cryThreshold2)   return ClampStage( 2);
        else if (t >= cryThreshold1)   return ClampStage( 1);
        else if (t <= relaxThreshold2) return ClampStage(-2);
        else if (t <= relaxThreshold1) return ClampStage(-1);
        else                           return ClampStage( 0);
    }

    // === Accumulate：把輸入當增量，加總＋衰減＋夾限，再轉段位 ===
    private int UpdateAccumulateAndMapToStage(float raw)
    {
        // 死區：小雜訊忽略
        float v = Mathf.Abs(raw) < deadzone ? 0f : raw;

        // 轉增量
        float delta = Mathf.Clamp(v * gain, -maxAbsDeltaPerTick, +maxAbsDeltaPerTick);
        _acc += delta;

        // 自然衰減（往 0 回歸）
        if (decayPerSecond > 0f)
        {
            float dec = decayPerSecond * Time.deltaTime;
            if (_acc > 0) _acc = Mathf.Max(0f, _acc - dec);
            else if (_acc < 0) _acc = Mathf.Min(0f, _acc + dec);
        }

        // 夾限：把連續值夾在 [-2, +2]（或你想要的範圍）
        _acc = Mathf.Clamp(_acc, minStage, maxStage);

        // 連續值轉「整數段位」：若尚貼近上一段位，則維持原值避免抖動
        var candidate = Mathf.Round(_acc);
        if (Mathf.Abs(_acc - _lastStage) < snapHysteresis)
            candidate = _lastStage;

        return ClampStage((int)candidate);
    }

    // === 防抖（對整數段位） ===
    private void TrySetStageWithDebounce(int targetStage)
    {
        if (targetStage == _lastStage) { _pendingStage = targetStage; _pendingCount = 0; return; }

        if (targetStage != _pendingStage) { _pendingStage = targetStage; _pendingCount = 1; }
        else
        {
            _pendingCount++;
            if (_pendingCount >= Mathf.Max(1, requiredConsecutive))
                SetStageImmediate(targetStage);
        }
    }

    private void SetStageImmediate(int stage)
    {
        int clamped = ClampStage(stage);
        if (logStageChanges)
            RuntimeLog.Info($"[RunAI] stage -> {clamped}");

        bool changed = _lastStage != clamped;
        _lastStage = clamped;
        _pendingStage = _lastStage; _pendingCount = 0;
        if (animator) animator.SetInteger(stageParam, _lastStage);
        else if (logStageChanges)
            RuntimeLog.Warning("[RunAI] animator reference missing");

        if (changed)
            StageChanged?.Invoke(_lastStage);
    }

    private int ClampStage(int s) => Mathf.Clamp(s, minStage, maxStage);
}
