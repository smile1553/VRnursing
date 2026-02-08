using UnityEngine;
using UnityEngine.UI;

public class ScenarioQuizSummary : MonoBehaviour
{
    public ScenarioController controller;
    public Text questionText;
    public Text answerText;
    public Text correctAnswerText;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;

    void Awake()
    {
        if (!controller)
            controller = FindObjectOfType<ScenarioController>();
    }

    void OnEnable()
    {
        if (controller != null)
            controller.quizAnswered.AddListener(OnQuiz);
    }

    void OnDisable()
    {
        if (controller != null)
            controller.quizAnswered.RemoveListener(OnQuiz);
    }

    void OnQuiz(ScenarioQuiz quiz, int selectedIndex, bool correct)
    {
        if (questionText)
            questionText.text = quiz?.question ?? "問題";
        if (answerText)
        {
            answerText.text = correct ? "答對" : "答錯";
            var graphic = answerText.GetComponent<Graphic>();
            if (graphic)
                graphic.color = correct ? correctColor : wrongColor;
        }
        if (correctAnswerText)
        {
            string correctText = (quiz != null && quiz.options != null && quiz.correctIndex < quiz.options.Length && quiz.correctIndex >= 0)
                ? quiz.options[quiz.correctIndex]
                : "";
            correctAnswerText.text = "正確答案: " + correctText;
        }
    }
}
