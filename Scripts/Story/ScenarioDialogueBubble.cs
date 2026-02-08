using UnityEngine;
using UnityEngine.UI;

public class ScenarioDialogueBubble : MonoBehaviour
{
    public ScenarioController controller;
    public BubbleBinding[] bubbles;
    public float fadeSpeed = 6f;

    BubbleBinding _active;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
    }

    void OnEnable()
    {
        if (controller != null)
            controller.StepChanged += HandleStep;
    }

    void OnDisable()
    {
        if (controller != null)
            controller.StepChanged -= HandleStep;
    }

    void Update()
    {
        foreach (var bubble in bubbles)
        {
            if (bubble == null || bubble.canvasGroup == null) continue;
            float target = bubble == _active ? 1f : 0f;
            bubble.canvasGroup.alpha = Mathf.MoveTowards(bubble.canvasGroup.alpha, target, Time.deltaTime * fadeSpeed);
            bool visible = bubble.canvasGroup.alpha > 0.01f;
            if (bubble.canvasGroup.gameObject.activeSelf != visible)
                bubble.canvasGroup.gameObject.SetActive(visible);
        }
    }

    void HandleStep(ScenarioStep step)
    {
        if (step == null)
        {
            _active = null;
            return;
        }

        _active = FindBubble(step.speaker);
        if (_active != null && _active.text != null)
            _active.text.text = step.dialogue;
    }

    BubbleBinding FindBubble(ScenarioSpeaker speaker)
    {
        if (bubbles == null) return null;
        for (int i = 0; i < bubbles.Length; i++)
        {
            var b = bubbles[i];
            if (b != null && b.speaker == speaker)
                return b;
        }
        return null;
    }
}

[System.Serializable]
public class BubbleBinding
{
    public ScenarioSpeaker speaker;
    public CanvasGroup canvasGroup;
    public Text text;
}
