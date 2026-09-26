using System;
using System.IO;
using UnityEngine;

public static class PendingResultStore
{
    const string FileName = "pending_student_result.json";
    const string TempSuffix = ".tmp";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool Exists => File.Exists(FilePath) || File.Exists(FilePath + TempSuffix);

    public static bool TryLoad(out FrozenStudentResult result, out string error)
    {
        result = null;
        error = string.Empty;
        try
        {
            string path = FilePath;
            string tempPath = path + TempSuffix;
            if (!File.Exists(path) && !File.Exists(tempPath))
                return false;

            string sourcePath = File.Exists(path) ? path : tempPath;
            string json = File.ReadAllText(sourcePath);
            result = JsonUtility.FromJson<FrozenStudentResult>(json);
            if (!IsValid(result))
            {
                result = null;
                error = "Pending result file is incomplete or invalid: " + FilePath;
                return false;
            }
            if (sourcePath == tempPath)
                File.Move(tempPath, path);
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to load pending result: " + exception.Message;
            return false;
        }
    }

    public static bool TrySaveNew(FrozenStudentResult result, out string error)
    {
        error = string.Empty;
        if (!IsValid(result))
        {
            error = "Cannot persist an incomplete frozen result.";
            return false;
        }

        string path = FilePath;
        string tempPath = path + TempSuffix;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                error = "A pending result file already exists and will not be overwritten: " + path;
                return false;
            }
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            File.WriteAllText(tempPath, JsonUtility.ToJson(result, true));
            File.Move(tempPath, path);
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to persist pending result: " + exception.Message;
            return false;
        }
    }

    public static bool TryDelete(out string error)
    {
        error = string.Empty;
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            string tempPath = FilePath + TempSuffix;
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return true;
        }
        catch (Exception exception)
        {
            error = "Result uploaded, but the pending file could not be deleted: " + exception.Message;
            return false;
        }
    }

    static bool IsValid(FrozenStudentResult result)
    {
        return result != null &&
            !string.IsNullOrWhiteSpace(result.sessionId) &&
            !string.IsNullOrWhiteSpace(result.resultId) &&
            !string.IsNullOrWhiteSpace(result.studentId) &&
            !string.IsNullOrWhiteSpace(result.loginTime) &&
            result.correctCount >= 0 && result.correctCount <= 8 &&
            result.toneScore >= 0 && result.toneScore <= 20;
    }
}
