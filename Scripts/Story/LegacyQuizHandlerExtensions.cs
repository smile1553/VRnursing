using UnityEngine.UI;

/// <summary>
/// Compatibility bridge for the legacy QuizHandler that lives outside the
/// __Project repository. It lets ScenarioController bind those existing panels
/// without requiring a duplicate quiz system.
/// </summary>
public static class LegacyQuizHandlerExtensions
{
    public static bool PrepareForScenario(
        this QuizHandler handler,
        ScenarioController controller,
        int correctIndex,
        int expectedOptionCount = -1)
    {
        if (handler == null || handler.currentQuizPanel == null || controller == null)
            return false;

        Button[] buttons = handler.currentQuizPanel.GetComponentsInChildren<Button>(true);
        if (expectedOptionCount > 0 && buttons.Length < expectedOptionCount)
            return false;

        // The first call only discovers and hides all panels. Bind listeners
        // when a concrete quiz (correctIndex >= 0) is about to be shown.
        if (correctIndex < 0)
            return true;

        int count = expectedOptionCount > 0
            ? UnityEngine.Mathf.Min(expectedOptionCount, buttons.Length)
            : buttons.Length;

        for (int i = 0; i < count; i++)
        {
            int captured = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => controller.SelectChoice(captured));
        }

        return count > 0;
    }
}
