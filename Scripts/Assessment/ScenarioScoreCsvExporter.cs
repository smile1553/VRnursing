using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ScenarioScoreCsvExporter : MonoBehaviour
{
    [Header("References")]
    public ScenarioScoreManager scoreManager;

    [Header("Export")]
    [Tooltip("可由 UI 或登入流程填入，匯出時會寫入報表。")]
    public string studentId;
    public bool autoExportOnCompleted = true;
    public bool exportHtmlTable = true;
    public bool appendClassSummary = true;
    public string fileNamePrefix = "scenario_score";
    public string classSummaryFileName = "class_summary.csv";

    public string LastExportPath { get; private set; }
    public string LastTableExportPath { get; private set; }
    public string LastClassSummaryPath { get; private set; }
    public string LastClassSummaryTablePath { get; private set; }
    public string LastClassSummaryExcelPath { get; private set; }

    void Awake()
    {
        if (!scoreManager)
            scoreManager = FindObjectOfType<ScenarioScoreManager>();
    }

    void OnEnable()
    {
        if (scoreManager != null)
            scoreManager.ScoreCompleted += HandleScoreCompleted;
    }

    void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.ScoreCompleted -= HandleScoreCompleted;
    }

    void HandleScoreCompleted(ScenarioScoreReport report)
    {
        if (autoExportOnCompleted)
            Export(report);
    }

    public void ExportLatest()
    {
        if (scoreManager?.CurrentReport != null)
            Export(scoreManager.CurrentReport);
    }

    public void Export(ScenarioScoreReport report)
    {
        if (report == null) return;

        string folder = Path.Combine(Application.persistentDataPath, "ScenarioScores");
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string prefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "scenario_score" : fileNamePrefix.Trim();
        string csvPath = Path.Combine(folder, $"{prefix}_{stamp}.csv");
        string tablePath = Path.Combine(folder, $"{prefix}_{stamp}.html");

        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(csvPath, BuildCsv(report), new UTF8Encoding(true));
            LastExportPath = csvPath;
            Debug.Log("[ScenarioScoreCsv] saved -> " + csvPath);

            if (exportHtmlTable)
            {
                File.WriteAllText(tablePath, BuildHtmlTable(report), new UTF8Encoding(true));
                LastTableExportPath = tablePath;
                Debug.Log("[ScenarioScoreTable] saved -> " + tablePath);
            }

            if (appendClassSummary)
                AppendClassSummary(folder, report);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ScenarioScoreCsv] export failed: " + ex.Message);
        }
    }

    string BuildCsv(ScenarioScoreReport report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("ExportedAt,StudentId,TotalScore,KnowledgeScore,ProcessScore,CommunicationScore,EmotionCareScore,QuestionNo,Question,Attempt,SelectedIndex,SelectedAnswer,CorrectIndex,CorrectAnswer,IsCorrect,QuestionScore");

        string exportedAt = DateTime.Now.ToString("o");
        if (report.quizzes == null || report.quizzes.Length == 0)
        {
            AppendCsvRow(csv, exportedAt, report, -1, null, null);
            return csv.ToString();
        }

        for (int i = 0; i < report.quizzes.Length; i++)
        {
            ScenarioQuizScore quiz = report.quizzes[i];
            if (quiz?.answerHistory == null || quiz.answerHistory.Count == 0)
            {
                AppendCsvRow(csv, exportedAt, report, i, quiz, null);
                continue;
            }

            foreach (ScenarioQuizAttempt attempt in quiz.answerHistory)
                AppendCsvRow(csv, exportedAt, report, i, quiz, attempt);
        }
        return csv.ToString();
    }

    void AppendClassSummary(string folder, ScenarioScoreReport report)
    {
        string fileName = string.IsNullOrWhiteSpace(classSummaryFileName) ? "class_summary.csv" : classSummaryFileName.Trim();
        string path = Path.Combine(folder, fileName);
        bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

        var csv = new StringBuilder();
        if (needsHeader)
        {
            csv.AppendLine("ExportedAt,StudentId,TotalScore,KnowledgeScore,ProcessScore,CommunicationScore,EmotionCareScore,CompletedRequiredSteps,RequiredSteps,MatchedCommunicationSteps,CommunicationSteps,MatchedEmotionCareSteps,EmotionCareSteps,QuizCorrectSummary,QuizScoreSummary,QuizAttemptSummary,WrongQuestions");
        }

        csv.AppendLine(BuildClassSummaryRow(DateTime.Now.ToString("o"), report));
        File.AppendAllText(path, csv.ToString(), new UTF8Encoding(true));
        LastClassSummaryPath = path;
        Debug.Log("[ScenarioScoreClassSummary] appended -> " + path);

        string tablePath = Path.ChangeExtension(path, ".html");
        File.WriteAllText(tablePath, BuildClassSummaryHtml(path), new UTF8Encoding(true));
        LastClassSummaryTablePath = tablePath;
        Debug.Log("[ScenarioScoreClassSummaryTable] saved -> " + tablePath);

        string excelPath = Path.ChangeExtension(path, ".xls");
        File.WriteAllText(excelPath, BuildClassSummaryExcel(path), new UTF8Encoding(true));
        LastClassSummaryExcelPath = excelPath;
        Debug.Log("[ScenarioScoreClassSummaryExcel] saved -> " + excelPath);
    }

    string BuildClassSummaryRow(string exportedAt, ScenarioScoreReport report)
    {
        string[] values =
        {
            exportedAt,
            studentId,
            report.total.ToString("0.##"),
            report.knowledge.ToString("0.##"),
            report.process.ToString("0.##"),
            report.communication.ToString("0.##"),
            report.emotionCare.ToString("0.##"),
            report.completedRequiredSteps.ToString(),
            report.requiredSteps.ToString(),
            report.matchedCommunicationSteps.ToString(),
            report.communicationSteps.ToString(),
            report.matchedEmotionCareSteps.ToString(),
            report.emotionCareSteps.ToString(),
            BuildQuizCorrectSummary(report),
            BuildQuizScoreSummary(report),
            BuildQuizAttemptSummary(report),
            BuildWrongQuestionSummary(report)
        };

        var row = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) row.Append(',');
            row.Append(EscapeCsv(values[i]));
        }
        return row.ToString();
    }

    void AppendCsvRow(StringBuilder csv, string exportedAt, ScenarioScoreReport report, int quizIndex, ScenarioQuizScore quiz, ScenarioQuizAttempt attempt)
    {
        string[] values =
        {
            exportedAt, studentId, report.total.ToString("0.##"), report.knowledge.ToString("0.##"),
            report.process.ToString("0.##"), report.communication.ToString("0.##"), report.emotionCare.ToString("0.##"),
            quiz != null ? GetQuizLabel(quizIndex, quiz) : string.Empty, quiz?.question, attempt != null ? attempt.attemptNumber.ToString() : string.Empty,
            attempt != null ? attempt.selectedIndex.ToString() : string.Empty, attempt?.selectedAnswer,
            quiz != null ? quiz.correctIndex.ToString() : string.Empty, GetCorrectAnswer(quiz, attempt),
            attempt != null ? attempt.correct.ToString() : string.Empty,
            quiz != null ? CalculateAttemptQuestionScore(report, attempt).ToString("0.##") : string.Empty
        };
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) csv.Append(',');
            csv.Append(EscapeCsv(values[i]));
        }
        csv.AppendLine();
    }

    string BuildHtmlTable(ScenarioScoreReport report)
    {
        var html = new StringBuilder();
        string exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"zh-Hant\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<title>VR 兒童生命徵象評分報表</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:'Microsoft JhengHei',Arial,sans-serif;margin:24px;color:#222}");
        html.AppendLine("h1{font-size:22px;margin:0 0 12px}");
        html.AppendLine(".summary{margin:0 0 16px;line-height:1.8}");
        html.AppendLine("table{border-collapse:collapse;width:100%;font-size:14px}");
        html.AppendLine("th,td{border:1px solid #999;padding:8px;vertical-align:top}");
        html.AppendLine("th{background:#f0f3f8;text-align:left}");
        html.AppendLine("tr.wrong-row{background:#fff5f5}");
        html.AppendLine(".correct{color:#0a7a32;font-weight:bold}");
        html.AppendLine(".wrong{color:#b00020;font-weight:bold}");
        html.AppendLine(".scoring{margin:16px 0;padding:12px 14px;background:#fffdf2;border:1px solid #d8c36a;line-height:1.8}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<h1>VR 兒童生命徵象評分報表</h1>");
        html.AppendLine("<div class=\"summary\">");
        html.AppendLine($"匯出時間：{HtmlEscape(exportedAt)}<br>");
        html.AppendLine($"學生 ID：{HtmlEscape(studentId)}<br>");
        html.AppendLine($"總分：{report.total:0.##}　知識：{report.knowledge:0.##}　流程：{report.process:0.##}　溝通：{report.communication:0.##}　情緒照護：{report.emotionCare:0.##}");
        html.AppendLine("</div>");
        html.AppendLine(BuildScoringGuideHtml(report));
        html.AppendLine("<table>");
        html.AppendLine("<thead><tr><th>題號</th><th>題目</th><th>第幾次作答</th><th>學生答案</th><th>正確答案</th><th>是否正確</th><th>本題得分</th></tr></thead>");
        html.AppendLine("<tbody>");

        if (report.quizzes == null || report.quizzes.Length == 0)
        {
            AppendHtmlRow(html, report, -1, null, null);
        }
        else
        {
            for (int i = 0; i < report.quizzes.Length; i++)
            {
                ScenarioQuizScore quiz = report.quizzes[i];
                if (quiz?.answerHistory == null || quiz.answerHistory.Count == 0)
                {
                    AppendHtmlRow(html, report, i, quiz, null);
                    continue;
                }

                foreach (ScenarioQuizAttempt attempt in quiz.answerHistory)
                    AppendHtmlRow(html, report, i, quiz, attempt);
            }
        }

        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    void AppendHtmlRow(StringBuilder html, ScenarioScoreReport report, int quizIndex, ScenarioQuizScore quiz, ScenarioQuizAttempt attempt)
    {
        string selectedAnswer = attempt != null
            ? FormatAnswer(attempt.selectedIndex, attempt.selectedAnswer)
            : string.Empty;
        string correctAnswer = quiz != null
            ? FormatAnswer(quiz.correctIndex, GetCorrectAnswer(quiz, attempt))
            : string.Empty;
        string isCorrect = attempt == null ? string.Empty : attempt.correct ? "正確" : "錯誤";
        string correctClass = attempt == null ? string.Empty : attempt.correct ? "correct" : "wrong";

        html.AppendLine(attempt != null && !attempt.correct ? "<tr class=\"wrong-row\">" : "<tr>");
        html.AppendLine($"<td>{HtmlEscape(quiz != null ? GetQuizLabel(quizIndex, quiz) : string.Empty)}</td>");
        html.AppendLine($"<td>{HtmlEscape(quiz?.question)}</td>");
        html.AppendLine($"<td>{HtmlEscape(attempt != null ? attempt.attemptNumber.ToString() : string.Empty)}</td>");
        html.AppendLine($"<td class=\"{correctClass}\">{HtmlEscape(selectedAnswer)}</td>");
        html.AppendLine($"<td>{HtmlEscape(correctAnswer)}</td>");
        html.AppendLine($"<td class=\"{correctClass}\">{HtmlEscape(isCorrect)}</td>");
        html.AppendLine($"<td>{HtmlEscape(quiz != null ? CalculateAttemptQuestionScore(report, attempt).ToString("0.##") + " 分" : string.Empty)}</td>");
        html.AppendLine("</tr>");
    }

    static string FormatAnswer(int index, string answer)
    {
        if (string.IsNullOrEmpty(answer)) return string.Empty;
        return $"{IndexToLetter(index)}. {answer}";
    }

    static string IndexToLetter(int index)
    {
        return index >= 0 && index < 26 ? ((char)('A' + index)).ToString() : index.ToString();
    }

    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string GetCorrectAnswer(ScenarioQuizScore quiz, ScenarioQuizAttempt attempt)
    {
        if (!string.IsNullOrEmpty(attempt?.correctAnswer))
            return attempt.correctAnswer;

        if (quiz?.options != null && quiz.correctIndex >= 0 && quiz.correctIndex < quiz.options.Length)
            return quiz.options[quiz.correctIndex];

        return string.Empty;
    }

    static string BuildQuizCorrectSummary(ScenarioScoreReport report)
    {
        if (report?.quizzes == null || report.quizzes.Length == 0) return string.Empty;

        var summary = new StringBuilder();
        for (int i = 0; i < report.quizzes.Length; i++)
        {
            ScenarioQuizScore quiz = report.quizzes[i];
            if (i > 0) summary.Append("；");
            summary.Append(GetQuizLabel(i, quiz));
            summary.Append(quiz != null && quiz.correct ? "：對" : "：錯");
        }
        return summary.ToString();
    }

    static string BuildQuizAttemptSummary(ScenarioScoreReport report)
    {
        if (report?.quizzes == null || report.quizzes.Length == 0) return string.Empty;

        var summary = new StringBuilder();
        for (int i = 0; i < report.quizzes.Length; i++)
        {
            ScenarioQuizScore quiz = report.quizzes[i];
            if (i > 0) summary.Append("；");
            summary.Append(GetQuizLabel(i, quiz));
            summary.Append("：");
            summary.Append(quiz != null ? Mathf.Max(quiz.attempts, quiz.answerHistory != null ? quiz.answerHistory.Count : 0) : 0);
            summary.Append("次");
        }
        return summary.ToString();
    }

    string BuildQuizScoreSummary(ScenarioScoreReport report)
    {
        if (report?.quizzes == null || report.quizzes.Length == 0) return string.Empty;

        var summary = new StringBuilder();
        for (int i = 0; i < report.quizzes.Length; i++)
        {
            ScenarioQuizScore quiz = report.quizzes[i];
            if (i > 0) summary.Append("；");
            summary.Append(GetQuizLabel(i, quiz));
            summary.Append("：");
            summary.Append(CalculateQuizScore(report, quiz).ToString("0.##"));
            summary.Append("分");
        }
        return summary.ToString();
    }

    static string BuildWrongQuestionSummary(ScenarioScoreReport report)
    {
        if (report?.quizzes == null || report.quizzes.Length == 0) return string.Empty;

        var summary = new StringBuilder();
        for (int i = 0; i < report.quizzes.Length; i++)
        {
            ScenarioQuizScore quiz = report.quizzes[i];
            if (quiz == null || quiz.correct) continue;

            if (summary.Length > 0) summary.Append("；");
            summary.Append(GetQuizLabel(i, quiz));
            if (!string.IsNullOrEmpty(quiz.question))
            {
                summary.Append(" ");
                summary.Append(quiz.question);
            }
        }
        return summary.ToString();
    }

    static string GetQuizLabel(int index, ScenarioQuizScore quiz)
    {
        return index >= 0 ? $"第{index + 1}題" : string.Empty;
    }

    float CalculateQuizScore(ScenarioScoreReport report, ScenarioQuizScore quiz)
    {
        if (report?.quizzes == null || report.quizzes.Length == 0 || quiz == null || !quiz.correct)
            return 0f;

        return GetQuestionFullScore(report) * Mathf.Clamp01(quiz.multiplier);
    }

    float CalculateAttemptQuestionScore(ScenarioScoreReport report, ScenarioQuizAttempt attempt)
    {
        if (attempt == null || !attempt.correct)
            return 0f;

        return GetQuestionFullScore(report) * GetAttemptMultiplier(attempt.attemptNumber);
    }

    float GetQuestionFullScore(ScenarioScoreReport report)
    {
        int quizCount = report?.quizzes != null ? report.quizzes.Length : 0;
        if (quizCount <= 0) return 0f;

        ScenarioScoreProfile profile = scoreManager != null ? scoreManager.profile : null;
        float knowledgeWeight = profile ? profile.knowledgeWeight : 40f;
        return knowledgeWeight / quizCount;
    }

    float GetAttemptMultiplier(int attemptNumber)
    {
        ScenarioScoreProfile profile = scoreManager != null ? scoreManager.profile : null;
        if (attemptNumber <= 1) return 1f;
        if (attemptNumber == 2) return profile ? profile.secondAttemptMultiplier : 0.6f;
        return profile ? profile.laterAttemptMultiplier : 0.3f;
    }

    string BuildClassSummaryHtml(string csvPath)
    {
        string[] lines = File.Exists(csvPath) ? File.ReadAllLines(csvPath, Encoding.UTF8) : new string[0];
        var html = new StringBuilder();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"zh-Hant\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<title>VR 兒童生命徵象班級總表</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:'Microsoft JhengHei',Arial,sans-serif;margin:24px;color:#222}");
        html.AppendLine("h1{font-size:22px;margin:0 0 12px}");
        html.AppendLine(".note{margin:0 0 16px;color:#555}");
        html.AppendLine("table{border-collapse:collapse;width:100%;font-size:13px}");
        html.AppendLine("th,td{border:1px solid #999;padding:8px;vertical-align:top}");
        html.AppendLine("th{background:#f0f3f8;text-align:left;white-space:nowrap}");
        html.AppendLine("td.score{text-align:right;white-space:nowrap}");
        html.AppendLine(".wrong{color:#b00020;font-weight:bold}");
        html.AppendLine(".scoring{margin:16px 0;padding:12px 14px;background:#fffdf2;border:1px solid #d8c36a;line-height:1.8}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<h1>VR 兒童生命徵象班級總表</h1>");
        html.AppendLine($"<p class=\"note\">更新時間：{HtmlEscape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}</p>");
        html.AppendLine(BuildScoringGuideHtml(null));
        html.AppendLine("<table>");

        if (lines.Length > 0)
        {
            string[] headers = SplitCsvLine(lines[0]);
            var visibleIndexes = new List<int>();
            for (int i = 0; i < headers.Length; i++)
                if (ShouldShowInClassSummaryHtml(headers[i]))
                    visibleIndexes.Add(i);

            html.AppendLine("<thead><tr>");
            foreach (int index in visibleIndexes)
                html.AppendLine($"<th>{HtmlEscape(ToClassSummaryHeaderLabel(headers[index]))}</th>");
            html.AppendLine("</tr></thead>");
            html.AppendLine("<tbody>");

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = SplitCsvLine(lines[i]);
                html.AppendLine("<tr>");
                foreach (int c in visibleIndexes)
                {
                    string value = c < cells.Length ? cells[c] : string.Empty;
                    string css = IsScoreColumn(headers[c]) ? " class=\"score\"" : string.Empty;
                    string renderedValue = RenderClassSummaryCell(headers[c], value);
                    html.AppendLine($"<td{css}>{renderedValue}</td>");
                }
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody>");
        }

        html.AppendLine("</table>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    static string BuildClassSummaryExcel(string csvPath)
    {
        string[] lines = File.Exists(csvPath) ? File.ReadAllLines(csvPath, Encoding.UTF8) : new string[0];
        if (lines.Length == 0) return string.Empty;

        string[] headers = SplitCsvLine(lines[0]);
        var visibleIndexes = new List<int>();
        for (int i = 0; i < headers.Length; i++)
            if (ShouldShowInClassSummaryHtml(headers[i]))
                visibleIndexes.Add(i);

        var tsv = new StringBuilder();
        for (int i = 0; i < visibleIndexes.Count; i++)
        {
            if (i > 0) tsv.Append('\t');
            tsv.Append(ToExcelHeaderLabel(headers[visibleIndexes[i]]));
        }
        tsv.AppendLine();

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex])) continue;
            string[] cells = SplitCsvLine(lines[lineIndex]);

            for (int i = 0; i < visibleIndexes.Count; i++)
            {
                int cellIndex = visibleIndexes[i];
                if (i > 0) tsv.Append('\t');

                string value = cellIndex < cells.Length ? cells[cellIndex] : string.Empty;
                tsv.Append(EscapeTsv(ToExcelCellValue(headers[cellIndex], value)));
            }
            tsv.AppendLine();
        }

        return tsv.ToString();
    }

    static string ToExcelHeaderLabel(string header)
    {
        switch (header)
        {
            case "ExportedAt": return "ExportedAt";
            case "StudentId": return "StudentID";
            case "TotalScore": return "Total";
            case "KnowledgeScore": return "Knowledge";
            case "ProcessScore": return "Process";
            case "CommunicationScore": return "Communication";
            case "EmotionCareScore": return "EmotionCare";
            case "QuizCorrectSummary": return "QCorrectSummary";
            case "QuizScoreSummary": return "QScoreSummary";
            case "QuizAttemptSummary": return "QAttemptSummary";
            case "WrongQuestions": return "WrongQuestions";
            default: return header;
        }
    }

    static string ToExcelCellValue(string header, string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (header == "QuizCorrectSummary")
            return value.Replace("第", "Q").Replace("題", "").Replace("：對", " OK").Replace("：錯", " WRONG").Replace("；", "; ");

        if (header == "QuizScoreSummary")
            return value.Replace("第", "Q").Replace("題：", " ").Replace("分", "").Replace("；", "; ");

        if (header == "QuizAttemptSummary")
            return value.Replace("第", "Q").Replace("題：", " ").Replace("次", " attempt").Replace("；", "; ");

        if (header == "WrongQuestions")
            return BuildWrongQuestionNumbers(value);

        return value;
    }

    static string BuildWrongQuestionNumbers(string value)
    {
        var wrong = new StringBuilder();
        for (int i = 1; i <= 30; i++)
        {
            if (!value.Contains($"第{i}題")) continue;
            if (wrong.Length > 0) wrong.Append("; ");
            wrong.Append("Q");
            wrong.Append(i);
        }
        return wrong.ToString();
    }

    static string EscapeTsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    string BuildScoringGuideHtml(ScenarioScoreReport report)
    {
        ScenarioScoreProfile profile = scoreManager != null ? scoreManager.profile : null;
        float knowledgeWeight = profile ? profile.knowledgeWeight : 40f;
        float processWeight = profile ? profile.processWeight : 25f;
        float communicationWeight = profile ? profile.communicationWeight : 25f;
        float emotionCareWeight = profile ? profile.emotionCareWeight : 10f;
        float secondAttemptMultiplier = profile ? profile.secondAttemptMultiplier : 0.6f;
        float laterAttemptMultiplier = profile ? profile.laterAttemptMultiplier : 0.3f;

        var html = new StringBuilder();
        html.AppendLine("<div class=\"scoring\">");
        html.AppendLine("<strong>評分方式：</strong><br>");
        if (report?.quizzes != null && report.quizzes.Length > 0)
        {
            float questionFullScore = knowledgeWeight / report.quizzes.Length;
            html.AppendLine($"知識分數 {knowledgeWeight:0.##} 分：共 {report.quizzes.Length} 題，每題滿分 {questionFullScore:0.##} 分；第一次答對得 {questionFullScore:0.##} 分，第二次答對得 {(questionFullScore * secondAttemptMultiplier):0.##} 分，第三次以後答對得 {(questionFullScore * laterAttemptMultiplier):0.##} 分。<br>");
        }
        else
        {
            html.AppendLine($"知識分數 {knowledgeWeight:0.##} 分：依題目數平均分配；第一次答對得滿分，第二次答對得 {secondAttemptMultiplier:0.##} 倍分，第三次以後答對得 {laterAttemptMultiplier:0.##} 倍分。<br>");
        }
        html.AppendLine($"流程分數 {processWeight:0.##} 分：完成需要操作或回應的流程步驟比例。");
        if (report != null) html.AppendLine($"（目前 {report.completedRequiredSteps}/{report.requiredSteps}）");
        html.AppendLine("<br>");
        html.AppendLine($"溝通分數 {communicationWeight:0.##} 分：在有溝通條件的步驟中，符合關鍵字或語意意圖的比例。");
        if (report != null) html.AppendLine($"（目前 {report.matchedCommunicationSteps}/{report.communicationSteps}）");
        html.AppendLine("<br>");
        html.AppendLine($"情緒照護分數 {emotionCareWeight:0.##} 分：在安撫、降低害怕、鼓勵、角色扮演等情緒照護步驟中達成的比例。");
        if (report != null) html.AppendLine($"（目前 {report.matchedEmotionCareSteps}/{report.emotionCareSteps}）");
        html.AppendLine("<br>");
        html.AppendLine("紅色粗體代表錯誤作答或錯題項目。");
        html.AppendLine("</div>");
        return html.ToString();
    }

    static string[] SplitCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(ch);
            }
        }

        values.Add(value.ToString());
        return values.ToArray();
    }

    static string ToClassSummaryHeaderLabel(string header)
    {
        switch (header)
        {
            case "ExportedAt": return "匯出時間";
            case "StudentId": return "學生 ID";
            case "TotalScore": return "總分";
            case "KnowledgeScore": return "知識";
            case "ProcessScore": return "流程";
            case "CommunicationScore": return "溝通";
            case "EmotionCareScore": return "情緒照護";
            case "CompletedRequiredSteps": return "完成必要步驟";
            case "RequiredSteps": return "必要步驟總數";
            case "MatchedCommunicationSteps": return "達成溝通步驟";
            case "CommunicationSteps": return "溝通步驟總數";
            case "MatchedEmotionCareSteps": return "達成情緒照護";
            case "EmotionCareSteps": return "情緒照護總數";
            case "QuizCorrectSummary": return "每題對錯";
            case "QuizScoreSummary": return "每題得分";
            case "QuizAttemptSummary": return "作答次數";
            case "WrongQuestions": return "錯題清單";
            default: return header;
        }
    }

    static bool IsScoreColumn(string header)
    {
        return header == "TotalScore" ||
            header == "KnowledgeScore" ||
            header == "ProcessScore" ||
            header == "CommunicationScore" ||
            header == "EmotionCareScore";
    }

    static bool ShouldShowInClassSummaryHtml(string header)
    {
        return header == "ExportedAt" ||
            header == "StudentId" ||
            header == "TotalScore" ||
            header == "KnowledgeScore" ||
            header == "ProcessScore" ||
            header == "CommunicationScore" ||
            header == "EmotionCareScore" ||
            header == "QuizCorrectSummary" ||
            header == "QuizScoreSummary" ||
            header == "QuizAttemptSummary" ||
            header == "WrongQuestions";
    }

    static string RenderClassSummaryCell(string header, string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (header == "WrongQuestions")
            return $"<span class=\"wrong\">{HtmlEscape(value).Replace("；", "<br>")}</span>";

        if (header == "QuizCorrectSummary")
        {
            string[] parts = value.Split('；');
            var html = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) html.Append("<br>");
                string escaped = HtmlEscape(parts[i]);
                html.Append(parts[i].Contains("：錯") ? $"<span class=\"wrong\">{escaped}</span>" : escaped);
            }
            return html.ToString();
        }

        return HtmlEscape(value).Replace("；", "<br>");
    }

    static string HtmlEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
