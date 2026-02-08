using UnityEngine;
using UnityEngine.UI;

public class EmotionHud : MonoBehaviour
{
    public EmotionStateManager manager;
    public Slider tensionSlider;
    public float sliderMin = -5f;
    public float sliderMax = 5f;
    public Text tensionText;
    public Text stageText;
    public Text intentText;
    public Image stageIndicator;
    public Color calmColor = Color.cyan;
    public Color neutralColor = Color.white;
    public Color anxiousColor = Color.red;

    void Awake()
    {
        if (!manager)
            manager = FindObjectOfType<EmotionStateManager>();
    }

    void OnEnable()
    {
        if (manager != null)
            manager.OnEmotionChanged += HandleEmotionChanged;
        UpdateVisual(manager?.Current);
    }

    void OnDisable()
    {
        if (manager != null)
            manager.OnEmotionChanged -= HandleEmotionChanged;
    }

    void HandleEmotionChanged(EmotionSnapshot snapshot)
    {
        UpdateVisual(snapshot);
    }

    void UpdateVisual(EmotionSnapshot snapshot)
    {
        if (tensionSlider)
        {
            tensionSlider.minValue = sliderMin;
            tensionSlider.maxValue = sliderMax;
            tensionSlider.value = snapshot?.tension ?? 0f;
        }

        if (tensionText)
            tensionText.text = snapshot != null ? snapshot.tension.ToString("0.00") : "--";

        if (stageText)
            stageText.text = snapshot != null ? $"Stage {snapshot.stage}" : "Stage --";

        if (intentText)
        {
            var intent = snapshot?.llm?.intent ?? "?";
            var sentiment = snapshot?.llm?.sentiment ?? "?";
            intentText.text = $"Intent: {intent}\nSentiment: {sentiment}";
        }

        if (stageIndicator)
        {
            var color = neutralColor;
            if (snapshot != null)
            {
                if (snapshot.stage >= manager.anxiousStage)
                    color = anxiousColor;
                else if (snapshot.stage <= manager.calmStage)
                    color = calmColor;
            }
            stageIndicator.color = color;
        }
    }
}
