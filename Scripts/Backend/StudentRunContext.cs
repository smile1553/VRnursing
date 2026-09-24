using System;

[Serializable]
public sealed class StudentRunContext
{
    static readonly StudentRunContext _current = new StudentRunContext();

    public static StudentRunContext Current => _current;

    public string SessionId;
    public string SessionCreatedAt;
    public string ResultId;
    public string StudentId;
    public string LoginTime;
    public bool ResultSubmitted;
    public bool IsSubmitting;
    public bool StudentRunAccepted;
    public bool ScenarioCompleted;
    public bool HasToneScore;
    public int ToneScore;
    public FrozenStudentResult PendingResult;

    public bool HasSession => !string.IsNullOrWhiteSpace(SessionId);
    public bool HasStudentRun => HasSession &&
        !string.IsNullOrWhiteSpace(ResultId) &&
        !string.IsNullOrWhiteSpace(StudentId) &&
        !string.IsNullOrWhiteSpace(LoginTime);
    public bool HasPendingResult => PendingResult != null;

    public void SetSession(string sessionId, string createdAt)
    {
        SessionId = sessionId ?? string.Empty;
        SessionCreatedAt = createdAt ?? string.Empty;
        ClearStudentRun();
    }

    public void ClearSession()
    {
        SessionId = string.Empty;
        SessionCreatedAt = string.Empty;
        ClearStudentRun();
    }

    public void BeginStudentRun(string studentId)
    {
        ResultId = Guid.NewGuid().ToString();
        StudentId = (studentId ?? string.Empty).Trim();
        LoginTime = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        ResultSubmitted = false;
        IsSubmitting = false;
        StudentRunAccepted = false;
        ScenarioCompleted = false;
        PendingResult = null;
        HasToneScore = false;
        ToneScore = 0;
    }

    public bool TrySetToneScore(int score)
    {
        if (score < 0 || score > 20)
            return false;

        ToneScore = score;
        HasToneScore = true;
        return true;
    }

    public FrozenStudentResult FreezeResult(int correctCount, int toneScore)
    {
        if (HasPendingResult)
            return PendingResult;

        PendingResult = new FrozenStudentResult
        {
            sessionId = SessionId,
            resultId = ResultId,
            studentId = StudentId,
            loginTime = LoginTime,
            correctCount = correctCount,
            toneScore = toneScore
        };
        return PendingResult;
    }

    public void RestorePendingResult(FrozenStudentResult result)
    {
        if (result == null) return;
        SessionId = result.sessionId ?? string.Empty;
        ResultId = result.resultId ?? string.Empty;
        StudentId = result.studentId ?? string.Empty;
        LoginTime = result.loginTime ?? string.Empty;
        PendingResult = result;
        ResultSubmitted = false;
        IsSubmitting = false;
        StudentRunAccepted = true;
        ScenarioCompleted = true;
        HasToneScore = true;
        ToneScore = result.toneScore;
    }

    public void ClearStudentRun()
    {
        ResultId = string.Empty;
        StudentId = string.Empty;
        LoginTime = string.Empty;
        ResultSubmitted = false;
        IsSubmitting = false;
        StudentRunAccepted = false;
        ScenarioCompleted = false;
        PendingResult = null;
        HasToneScore = false;
        ToneScore = 0;
    }
}

[Serializable]
public sealed class FrozenStudentResult
{
    public string sessionId;
    public string resultId;
    public string studentId;
    public string loginTime;
    public int correctCount;
    public int toneScore;
}
