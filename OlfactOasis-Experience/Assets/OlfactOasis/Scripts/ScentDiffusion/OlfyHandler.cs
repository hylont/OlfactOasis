using EditorAttributes;
using Olfy;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class OlfyHandler : MonoBehaviour, IScentDiffuser
{
    [Header("Dependencies")]
    [SerializeField] private OlfyManager _olfyManager;
    [SerializeField] private BleManager _bluetoothManager;

    [Header("Config")]
    [SerializeField] int _nbSlots = 3;

    [Header("Callbacks")]
    public UnityEvent OnOlfyReady;
    bool _readyNotified = false;

    [Header("Debug")]

    [SerializeField] private TextMeshProUGUI _debugText;

    [SerializeField] private SerializableDictionaryBase<EScentSlotStatus, Color> _statusColors = new()
    {
        { EScentSlotStatus.Unknown, Color.gray },
        { EScentSlotStatus.Ready, Color.green },
        { EScentSlotStatus.Cooldown, Color.yellow },
        { EScentSlotStatus.Empty, Color.red },
        { EScentSlotStatus.Error, Color.magenta },
        { EScentSlotStatus.Diffusing, Color.cyan }
    };
    [SerializeField] bool _ignoreNotConnected = false;
    [SerializeField] SerializableDictionaryBase<int, Image> _slotsVisuals;

    Dictionary<int, ScentSlotData> _slots = new();

    public static OlfyHandler Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // The Olfy prefab (this object's root) is DontDestroyOnLoad, but it's still
            // part of the scene file, so every scene (re)load instantiates a throwaway
            // duplicate that must not steal the singleton slot from the persisted one.
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        Init();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // This instance survives scene reloads, so OnOlfyReady already fired once and
        // its listeners belonged to the scene that just got unloaded. Re-arm it so the
        // freshly loaded scene's listeners get notified too (if Olfy is still ready).
        _readyNotified = false;
    }

    private void Init()
    {
        if (_olfyManager == null)
        {
            LLogger.E("OlfyManager reference is missing!");
            if (_debugText != null)
            {
                _debugText.text = "OlfyManager reference is missing!";
            }
            return;
        }

        for (int idxSlot = 1; idxSlot <= _nbSlots; idxSlot++)
        {
            _slots.Add(idxSlot, new ScentSlotData(EScentSlotStatus.Unknown));
        }
    }

    void Update()
    {
        if(!_readyNotified && _olfyManager.isReady)
        {
            _readyNotified = true;
            OnOlfyReady?.Invoke();
        }

        UpdateSlotColors();
    }

    void UpdateSlotColors()
    {
        foreach(var slot in _slots)
        {
            _statusColors.TryGetValue(slot.Value.Status, out Color newColor);
            _slotsVisuals[slot.Key].color = newColor;
        }
    }

    public bool RequestDiffusion(ScentDiffusionParameters parameters)
    {

        if (OlfyManager.Instance.isReady)
        {
            HandleDiffusion(parameters);

            _olfyManager.SendSmellToOlfy(parameters.Duration, parameters.SlotIndex + "", (int)(parameters.Strength * 100), parameters.Frequency, false);
            
            string output = "Diffusion request sent to Olfy at "+DateTime.Now.ToString("HH:mm:ss")+"\nSlot " + parameters.SlotIndex + ", Strength " + (int)(parameters.Strength * 100) + ", Duration " + parameters.Duration + ", Frequency " + parameters.Frequency;
            if(_debugText != null) _debugText.text = output;
            LLogger.L(output);

            return true;
        }
        else
        {
            if (_ignoreNotConnected) HandleDiffusion(parameters);
            
            LLogger.E("Olfy was not ready");
            return false;
        }
    }

    private void HandleDiffusion(ScentDiffusionParameters parameters)
    {
        _slots[parameters.SlotIndex].Status = EScentSlotStatus.Diffusing;

        StartCoroutine(HandleStopDiffusion_Coroutine(parameters));
    }

    IEnumerator HandleStopDiffusion_Coroutine(ScentDiffusionParameters parameters)
    {
        yield return new WaitForSeconds(parameters.Duration);
        _slots[parameters.SlotIndex].Status = EScentSlotStatus.Ready;
    }

    public ScentDiffuserDeviceInfo GetDeviceStatus()
    {
        throw new NotImplementedException();
    }
}
