using EditorAttributes;
using Olfy;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class OlfyHandler : MonoBehaviour, IScentDiffuser
{

    [SerializeField]
    ScentDiffusionParameters _testScent = new ScentDiffusionParameters(1, .5f, 3f);

    [Button("Diffuse test")]
    void DiffuseTest()
    {
        RequestDiffusion(_testScent);
    }

    [Header("Dependencies")]
    [SerializeField] private OlfyManager _olfyManager;
    [SerializeField] private BleManager _bluetoothManager;

    [Header("Config")]
    [SerializeField] int _nbSlots = 3;

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

    void Start()
    {
        if (_olfyManager == null)
        {
            LLogger.E("OlfyManager reference is missing!");
            if(_debugText != null)
            {
                _debugText.text = "OlfyManager reference is missing!";
            }
            return;
        }

        for(int idxSlot = 1; idxSlot <= _nbSlots; idxSlot++)
        {
            _slots.Add(idxSlot, new ScentSlotData(EScentSlotStatus.Unknown));
        }
    }

    void Update()
    {
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

            BleManager.Instance.Diffuse((int)parameters.Duration, parameters.SlotIndex+"", (int)(parameters.Strength*100), parameters.Frequency, false);
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
