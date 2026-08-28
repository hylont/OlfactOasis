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
        INFO,
        WARNING,
        ERROR
    }

    private const string LOG_FOLDER_NAME = "Logs";
    private const string LOG_FILE_EXTENSION = ".log";
    private const int LOG_RETENTION_DAYS = 30;
    private const string LOG_LINE_FORMAT = "[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}";
    private const float SCREEN_LOG_DURATION_SECONDS = 5f;

    private static readonly object m_lock = new object();
    private static bool m_hasPurgedOldLogs = false;

    private static string LogDirectory => Path.Combine(Application.persistentDataPath, LOG_FOLDER_NAME);
    private static string CurrentLogFilePath => Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}{LOG_FILE_EXTENSION}");

    public static void L(string p_text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {p_text}", ESeverity.INFO);
    }

    public static void W(string p_text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {p_text}", ESeverity.WARNING);
    }

    public static void E(string p_text, [CallerMemberName] string callerName = "", [CallerFilePath] string file = "")
    {
        Log($"[{callerName.ToUpper()}:{Path.GetFileNameWithoutExtension(file).ToUpper()}] {p_text}", ESeverity.ERROR);
    }

    private static void Log(string p_text, ESeverity p_severity = ESeverity.INFO)
    {
        lock (m_lock)
        {
            string line = string.Format(LOG_LINE_FORMAT, DateTime.Now, p_severity, p_text);

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                if (!m_hasPurgedOldLogs)
                {
                    PurgeOldLogs();
                    m_hasPurgedOldLogs = true;
                }

                File.AppendAllText(CurrentLogFilePath, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLogger] Failed to write log entry: {e}");
            }

            switch (p_severity)
            {
                case ESeverity.WARNING:
                    Debug.LogWarning(line);
                    break;
                case ESeverity.ERROR:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
#if !UNITY_EDITOR
            LogOnScreen(p_text, p_severity);
#endif
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

    private static LLoggerGUI m_guiInstance;

    public static void LogOnScreen(string p_text, ESeverity p_severity = ESeverity.INFO)
    {
        if (m_guiInstance == null)
        {
            GameObject guiObject = new GameObject(nameof(LLoggerGUI));
            UnityEngine.Object.DontDestroyOnLoad(guiObject);
            m_guiInstance = guiObject.AddComponent<LLoggerGUI>();
        }

        m_guiInstance.ShowMessage(p_text, p_severity, SCREEN_LOG_DURATION_SECONDS);
    }

    private class LLoggerGUI : MonoBehaviour
    {
        private class ScreenMessage
        {
            public string Text;
            public ESeverity Severity;
            public float ExpireTime;
        }

        private readonly List<ScreenMessage> m_messages = new List<ScreenMessage>();

        public void ShowMessage(string p_text, ESeverity p_severity, float p_duration)
        {
            m_messages.Add(new ScreenMessage
            {
                Text = p_text,
                Severity = p_severity,
                ExpireTime = Time.time + p_duration
            });
        }

        private void Update()
        {
            m_messages.RemoveAll(message => Time.time >= message.ExpireTime);
        }

        private void OnGUI()
        {
            if (m_messages.Count == 0)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 500, Screen.height - 20));
            foreach (ScreenMessage message in m_messages)
            {
                Color previousColor = GUI.color;
                GUI.color = GetSeverityColor(message.Severity);
                GUILayout.Label($"[{message.Severity}] {message.Text}");
                GUI.color = previousColor;
            }
            GUILayout.EndArea();
        }

        private static Color GetSeverityColor(ESeverity p_severity)
        {
            switch (p_severity)
            {
                case ESeverity.WARNING:
                    return Color.yellow;
                case ESeverity.ERROR:
                    return Color.red;
                default:
                    return Color.white;
            }
        }

        [Button("Test Info")]
        private void TestInfo() => LLogger.LogOnScreen("This is an info test text. If you see this, everything is fine");


        [Button("Test Warn")]
        private void TestWarning() => LLogger.LogOnScreen("This is a warning test text. If you see this, everything is fine", ESeverity.WARNING);


        [Button("Test Error")]
        private void TestError() => LLogger.LogOnScreen("This is an error test text. If you see this, everything is fine", ESeverity.ERROR);
    }
}
