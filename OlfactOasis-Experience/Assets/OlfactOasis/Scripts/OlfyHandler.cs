using Olfy;
using RotaryHeart.Lib.SerializableDictionary;
using System;
using TMPro;
using UnityEngine;
using static LLogger;

public class OlfyHandler : MonoBehaviour
{
    public enum EScentSlotStatus
    {
        UNKNOWN, READY, COOLDOWN, EMPTY, ERROR, WORKING
    }



    [Serializable]
    public class ScentSlotData
    {
        public EScentSlotStatus Status = EScentSlotStatus.UNKNOWN;
        public TextMeshProUGUI DebugText;
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

        LLogger.L("Olfy handler initiated");
    }
}
