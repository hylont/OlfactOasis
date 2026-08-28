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
        public EScentSlotStatus Status = EScentSlotStatus.Unknown;
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
        { EScentSlotStatus.Unknown, Color.gray },
        { EScentSlotStatus.Ready, Color.green },
        { EScentSlotStatus.Cooldown, Color.yellow },
        { EScentSlotStatus.Empty, Color.red },
        { EScentSlotStatus.Error, Color.magenta },
        { EScentSlotStatus.Working, Color.cyan }
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

    public bool RequestDiffusion(ScentDiffusionParameters parameters)
    {
        if (OlfyManager.Instance.isReady)
        {
            BleManager.Instance.Diffuse((int)parameters.Duration, parameters.SlotIndex+"", (int)(parameters.Strength*100), parameters.Frequency, false);
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
