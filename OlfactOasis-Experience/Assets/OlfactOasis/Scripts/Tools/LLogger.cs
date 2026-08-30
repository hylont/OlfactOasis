using EditorAttributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class LLogger
{
    public enum ESeverity
    {
        Info,
        Warning,
        Error
    }

    private const string LOG_FOLDER_NAME = "Logs";
    private const string LOG_FILE_EXTENSION = ".log";
    private const int LOG_RETENTION_DAYS = 30;
    private const string LOG_LINE_FORMAT = "[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}";
    private const float SCREEN_LOG_DURATION_SECONDS = 5f;

    private static readonly object s_lock = new object();
    private static bool s_hasPurgedOldLogs = false;

    private static string LogDirectory => Path.Combine(Application.persistentDataPath, LOG_FOLDER_NAME);
    private static string CurrentLogFilePath => Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}{LOG_FILE_EXTENSION}");

    public static void L(string text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {text}", ESeverity.Info);
    }

    public static void W(string text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {text}", ESeverity.Warning);
    }

    public static void E(string text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {text}", ESeverity.Error);
    }

    private static void Log(string text, ESeverity severity = ESeverity.Info)
    {
        lock (s_lock)
        {
            string line = string.Format(LOG_LINE_FORMAT, DateTime.Now, severity, text);

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                if (!s_hasPurgedOldLogs)
                {
                    PurgeOldLogs();
                    s_hasPurgedOldLogs = true;
                }

                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLogger] Failed to write log entry: {e}");
            }

            switch (severity)
            {
                case ESeverity.Warning:
                    Debug.LogWarning(line);
                    break;
                case ESeverity.Error:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
            if(Application.isPlaying) LogOnScreenOnly(text, severity);
        }
    }

    private static void PurgeOldLogs()
    {
        DateTime cutoff = DateTime.Now.Date.AddDays(-LOG_RETENTION_DAYS);

        foreach (string filePath in Directory.GetFiles(LogDirectory, $"*{LOG_FILE_EXTENSION}"))
        {
            if (File.GetLastWriteTime(filePath) < cutoff)
            {
                File.Delete(filePath);
            }
        }
    }

    private static LLoggerGUI s_guiInstance;

    public static void LogOnScreenOnly(string text, ESeverity severity = ESeverity.Info)
    {
        if (s_guiInstance == null)
        {
            GameObject guiObject = new GameObject(nameof(LLoggerGUI));
            UnityEngine.Object.DontDestroyOnLoad(guiObject);
            s_guiInstance = guiObject.AddComponent<LLoggerGUI>();
        }

        s_guiInstance.ShowMessage(text, severity, SCREEN_LOG_DURATION_SECONDS);
    }

    private class LLoggerGUI : MonoBehaviour
    {
        private class ScreenMessage
        {
            public string Text;
            public ESeverity Severity;
            public float ExpireTime;
        }

        private readonly List<ScreenMessage> _messages = new List<ScreenMessage>();

        public void ShowMessage(string text, ESeverity severity, float duration)
        {
            _messages.Add(new ScreenMessage
            {
                Text = text,
                Severity = severity,
                ExpireTime = Time.time + duration
            });
        }

        private void Update()
        {
            _messages.RemoveAll(message => Time.time >= message.ExpireTime);
        }

        private void OnGUI()
        {
            if (_messages.Count == 0)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 500, Screen.height - 20));
            foreach (ScreenMessage message in _messages)
            {
                Color previousColor = GUI.color;
                GUI.color = GetSeverityColor(message.Severity);
                GUILayout.Label($"[{message.Severity}] {message.Text}");
                GUI.color = previousColor;
            }
            GUILayout.EndArea();
        }

        private static Color GetSeverityColor(ESeverity severity)
        {
            switch (severity)
            {
                case ESeverity.Warning:
                    return Color.yellow;
                case ESeverity.Error:
                    return Color.red;
                default:
                    return Color.white;
            }
        }

        [Button("Test Info")]
        private void TestInfo() => LLogger.LogOnScreenOnly("This is an info test text. If you see this, everything is fine");


        [Button("Test Warn")]
        private void TestWarning() => LLogger.LogOnScreenOnly("This is a warning test text. If you see this, everything is fine", ESeverity.Warning);


        [Button("Test Error")]
        private void TestError() => LLogger.LogOnScreenOnly("This is an error test text. If you see this, everything is fine", ESeverity.Error);
    }
}
