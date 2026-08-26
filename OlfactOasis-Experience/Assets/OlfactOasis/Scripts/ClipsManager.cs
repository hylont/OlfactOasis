using EditorAttributes;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

[Serializable]
public class AudioData
{
    public AudioClip Clip;
    [TextArea] public string asText;
}

[CreateAssetMenu(fileName = "ClipsManager", menuName = "OlfactOasis/Clips Manager")]
public class ClipsManager : ScriptableObject
{
    const string RESOURCE_PATH = "ClipsManager";

    [Header("Clips")]
    [SerializeField] SerializableDictionaryBase<string, AudioData> _clips;
    [SerializeField] AudioData _errorClip;

#if UNITY_EDITOR
    [Header("Editor tools")]
    [SerializeField] string _soundsFolder = "Assets/OlfactOasis/Sounds";
    [SerializeField] string _scriptsFolder = "Assets/OlfactOasis/Scripts";
#endif

    static ClipsManager s_instance;
    public static ClipsManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = Resources.Load<ClipsManager>(RESOURCE_PATH);
                if (s_instance == null) LLogger.E($"ClipsManager: no asset found at 'Resources/{RESOURCE_PATH}'");
            }

            return s_instance;
        }
    }

    public static AudioData GetClip(string p_clipID)
    {
        ClipsManager instance = Instance;
        if (instance == null)
        {
            LLogger.E("Instance is not ready !");
            if(instance._clips == null || instance._clips.Count == 0)
            {
                LLogger.E("There are no clips !");
                return instance._errorClip;
            }
        }

        if (instance._clips.TryGetValue(p_clipID, out AudioData data)) return data;

        LLogger.W($"ClipsManager: no clip registered for ID '{p_clipID}'");
        return instance._errorClip;
    }

#if UNITY_EDITOR
    static readonly Regex s_getClipCallRegex = new Regex(@"GetClip\s*\(\s*""([^""]*)""\s*\)", RegexOptions.Compiled);

    [Button("Check Clip Calls")]
    void CheckClipCalls()
    {
        if (_clips == null) _clips = new SerializableDictionaryBase<string, AudioData>();

        string[] scriptPaths = Directory.GetFiles(_scriptsFolder, "*.cs", SearchOption.AllDirectories);

        int callCount = 0;
        int missingCount = 0;

        foreach (string scriptPath in scriptPaths)
        {
            string[] lines = File.ReadAllLines(scriptPath);

            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in s_getClipCallRegex.Matches(lines[i]))
                {
                    callCount++;
                    string clipID = match.Groups[1].Value;

                    if (!_clips.ContainsKey(clipID))
                    {
                        missingCount++;
                        LLogger.E($"ClipsManager: '{clipID}' called in {scriptPath}:{i + 1} has no matching entry");
                    }
                }
            }
        }

        string log = $"ClipsManager: checked {callCount} GetClip call(s), {missingCount} missing";
        if (missingCount > 0) LLogger.W(log);
        else LLogger.L(log);
    }

    [Button("Update List")]
    void UpdateList(bool p_overwriteExisting = false)
    {
        if (_clips == null) _clips = new SerializableDictionaryBase<string, AudioData>();

        string soundsFolder = _soundsFolder.Replace('\\', '/').TrimEnd('/');
        string[] mp3Paths = Directory.GetFiles(soundsFolder, "*.mp3", SearchOption.AllDirectories);

        int addedCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;

        foreach (string mp3Path in mp3Paths)
        {
            string assetPath = mp3Path.Replace('\\', '/');
            string relativePath = assetPath.Substring(soundsFolder.Length).TrimStart('/');
            string withoutExtension = relativePath.Substring(0, relativePath.Length - Path.GetExtension(relativePath).Length);
            string clipID = withoutExtension.Replace('/', '.').ToLowerInvariant();

            bool alreadyExists = _clips.ContainsKey(clipID);
            if (alreadyExists && !p_overwriteExisting)
            {
                skippedCount++;
                continue;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                LLogger.E($"ClipsManager: could not load AudioClip at '{assetPath}'");
                continue;
            }

            _clips[clipID] = new AudioData { Clip = clip };

            if (alreadyExists) updatedCount++;
            else addedCount++;
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        LLogger.L($"ClipsManager: {addedCount} added, {updatedCount} updated, {skippedCount} skipped");
    }
#endif
}
