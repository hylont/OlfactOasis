using EditorAttributes;
using Olfy;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using TMPro;
using UnityEngine;

public class OlfyHandler : MonoBehaviour, IScentDiffuser
{

    [Serializable]
    public class ScentSlotData
    {
        public EScentSlotStatus Status = EScentSlotStatus.UNKNOWN;
        public TextMeshProUGUI DebugText;
    }

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

    [Header("Debug")]

    [SerializeField] private TextMeshProUGUI _debugText;

    [SerializeField] private SerializableDictionaryBase<EScentSlotStatus, Color> _statusColors = new()
    {
        { EScentSlotStatus.UNKNOWN, Color.gray },
        { EScentSlotStatus.READY, Color.green },
        { EScentSlotStatus.COOLDOWN, Color.yellow },
        { EScentSlotStatus.EMPTY, Color.red },
        { EScentSlotStatus.ERROR, Color.magenta },
        { EScentSlotStatus.WORKING, Color.cyan }
    };

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
    }

    public bool RequestDiffusion(ScentDiffusionParameters p_params)
    {
        if (OlfyManager.Instance.isReady)
        {
            BleManager.Instance.Diffuse((int)p_params.Duration, p_params.SlotIndex+"", (int)(p_params.Strength*100), p_params.Frequency, false);
            return true;
        }
        LLogger.E("Olfy was not ready");
        return false;
    }

    public ScentDiffuserDeviceInfo GetDeviceStatus()
    {
        throw new NotImplementedException();
    }
}
