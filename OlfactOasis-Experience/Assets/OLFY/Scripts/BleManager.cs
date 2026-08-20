using Olfy;
using System.Text;
using TMPro;
using UnityEngine;
public class BleManager : MonoBehaviour
{
    #region public members
    [HideInInspector] public string DeviceName = "OLFY-BLE";
    [HideInInspector] public string ServiceUUID = "013f5182-92dc-11ee-b9d1-0242ac120002";//via olfyblecommander
    [HideInInspector] public string CHARACTERISTIC_UUID = "17d2bd76-92dc-11ee-b9d1-0242ac120002";//via olfyblecommander
    public bool enableDebug = false;
    public TextMeshProUGUI StatusText;
    #endregion
    enum States
    {
        None,
        Scan,
        ScanRSSI,
        ReadRSSI,
        Connect,
        RequestMTU,
        Subscribe,
        Unsubscribe,
        Disconnect,
    }
    #region private members
    private bool _connected = false;
    private float _timeout = 0f;
    private States _state = States.None;
    private string _deviceAddress;
    private bool _foundOlfyUUID = false;
    private bool _rssiOnly = false;
    private int _rssi = 0;
    #endregion
    public static BleManager Instance = null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private string StatusMessage
    {
        set
        {
            BluetoothLEHardwareInterface.Log(value);
            StatusText.text = value;
            if(enableDebug)
                Debug.Log(value);
        }
    }

    void Reset()
    {
        _connected = false;
        _timeout = 0f;
        _state = States.None;
        _deviceAddress = null;
        _foundOlfyUUID = false;
        _rssi = 0;
    }

    void SetState(States newState, float timeout)
    {
        _state = newState;
        _timeout = timeout;
    }

    void StartProcess()
    {
        Reset();
        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            SetState(States.Scan, 0.1f);

        }, (error) =>
        {

            StatusMessage = "Error during initialize: " + error;
        });
    }

    // Use this for initialization
    void Start()
    {
        StartProcess();
    }

    void Update()
    {
        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout <= 0f)
            {
                _timeout = 0f;

                switch (_state)
                {
                    case States.None:
                        break;

                    case States.Scan:
                        StatusMessage = "Scanning for " + DeviceName;

                        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
                        {
                            // if your device does not advertise the rssi and manufacturer specific data
                            // then you must use this callback because the next callback only gets called
                            // if you have manufacturer specific data

                            if (!_rssiOnly)
                            {
                                if (name.Contains(DeviceName))
                                {
                                    StatusMessage = "Found " + name;

                                    // found a device with the name we want
                                    // this example does not deal with finding more than one
                                    _deviceAddress = address;
                                    SetState(States.Connect, 0.5f);
                                }
                            }

                        }, (address, name, rssi, bytes) =>
                        {

                            // use this one if the device responses with manufacturer specific data and the rssi

                            if (name.Contains(DeviceName))
                            {
                                StatusMessage = "Found " + name;

                                if (_rssiOnly)
                                {
                                    _rssi = rssi;
                                }
                                else
                                {
                                    // found a device with the name we want
                                    // this example does not deal with finding more than one
                                    _deviceAddress = address;
                                    SetState(States.Connect, 0.5f);
                                }
                            }

                        }, _rssiOnly); // this last setting allows RFduino to send RSSI without having manufacturer data

                        if (_rssiOnly)
                            SetState(States.ScanRSSI, 0.5f);
                        break;

                    case States.ScanRSSI:
                        break;

                    case States.ReadRSSI:
                        StatusMessage = $"Call Read RSSI";
                        BluetoothLEHardwareInterface.ReadRSSI(_deviceAddress, (address, rssi) =>
                        {
                            StatusMessage = $"Read RSSI: {rssi}";
                        });

                        SetState(States.ReadRSSI, 2f);
                        break;

                    case States.Connect:
                        StatusMessage = "Connecting...";

                        _foundOlfyUUID = false;
                        BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
                        {
                            StatusMessage = "Connected...";

                            BluetoothLEHardwareInterface.StopScan();

                            if (IsEqual(serviceUUID, ServiceUUID))
                            {
                                StatusMessage = "Found Service UUID";
                                _foundOlfyUUID = _foundOlfyUUID || IsEqual(characteristicUUID, CHARACTERISTIC_UUID);                             

                                if (_foundOlfyUUID)
                                {
                                    _connected = true;
                                    OlfyManager.Instance.isReady = true;
                                    SetState(States.RequestMTU, 2f);
                                }
                            }
                        });
                        break;

                    case States.RequestMTU:
                        Debug.Log("RequestMTU");
                        StatusMessage = "Requesting MTU";

                        BluetoothLEHardwareInterface.RequestMtu(_deviceAddress, 185, (address, newMTU) =>
                        {
                            StatusMessage = "MTU set to " + newMTU.ToString();
                            StatusMessage = "Prêt";
                            // SetState(States.Subscribe, 0.1f);
                        });
                        break;

                    case States.Subscribe:
                        Debug.Log("Subscribe");
                        StatusMessage = "Subscribing to characteristics...";

                        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, (notifyAddress, notifyCharacteristic) =>
                        {
                            StatusMessage = "Waiting for user action (1)...";
                            _state = States.None;

                            //// read the initial state of the button
                            //BluetoothLEHardwareInterface.ReadCharacteristic(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, (characteristic, bytes) =>
                            //{
                            //    ProcessButton(bytes);
                            //});

                            SetState(States.ReadRSSI, 1f);

                        }, (address, characteristicUUID, bytes) =>
                        {
                            if (_state != States.None)
                            {
                                StatusMessage = "Waiting for user action (2)...";

                                SetState(States.ReadRSSI, 1f);
                            }

                            //// we received some data from the device
                            //ProcessButton(bytes);
                        });
                        break;

                    case States.Unsubscribe:
                        Debug.Log("Unsubscribe");
                        BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, null);
                        SetState(States.Disconnect, 4f);
                        break;

                    case States.Disconnect:
                        Debug.Log("Disconnect");
                        StatusMessage = "Commanded disconnect.";

                        if (_connected)
                        {
                            BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
                            {
                                StatusMessage = "Device disconnected";
                                BluetoothLEHardwareInterface.DeInitialize(() =>
                                {
                                    _connected = false;
                                    _state = States.None;
                                });
                            });
                        }
                        else
                        {
                            BluetoothLEHardwareInterface.DeInitialize(() =>
                            {
                                _state = States.None;
                            });
                        }
                        break;
                }
            }
        }
    }

    public void Diffuse(int duration, string channel, int intensity, int freq, bool booster)
    {
        Debug.Log("Diffuse : " + channel);

        string str = "{ \"action\":\"diffuse\",\"duration\":" + duration + ",\"channel\":" + channel + ",\"intensity\":" + intensity + ",\"freq\":" + freq + ",\"booster\":" + booster + "}";
        SendString(str);
    }
    //public void GetBatt()
    //{
    //    Debug.Log("GetBatt");

    //    string str = @"{""action"":""getbatt""}";
    //    SendString(str);
    //    ReadString();
    //}

    string FullUUID(string uuid)
    {
        string fullUUID = uuid;
        if (fullUUID.Length == 4)
            fullUUID = "0000" + uuid + "-0000-1000-8000-00805f9b34fb";

        return fullUUID;
    }

    bool IsEqual(string uuid1, string uuid2)
    {
        if (uuid1.Length == 4)
            uuid1 = FullUUID(uuid1);
        if (uuid2.Length == 4)
            uuid2 = FullUUID(uuid2);

        return (uuid1.ToUpper().Equals(uuid2.ToUpper()));
    }
    public void ReadString()
    {
        BluetoothLEHardwareInterface.ReadCharacteristic(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, (characteristic, bytes) =>
        {
            string result = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log(result);
        });
    }
    void SendByte(byte value)
    {
        byte[] data = { value };
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, data, data.Length, true, (characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Write Succeeded");
        });
    }

    void SendString(string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, ServiceUUID, CHARACTERISTIC_UUID, data, data.Length, true, (characteristicUUID) =>
        {
            BluetoothLEHardwareInterface.Log("Write Succeeded");
        });
    }
    public void Disconnected()
    {
        if (_connected)
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
            {
                StatusMessage = "Device disconnected";
                BluetoothLEHardwareInterface.DeInitialize(() =>
                {
                    _connected = false;
                    _state = States.None;
                });
            });
        }
        else
        {
            BluetoothLEHardwareInterface.DeInitialize(() =>
            {
                _state = States.None;
            });
        }
    }
    private void OnApplicationQuit()
    {
        if (_connected)
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
            {
                StatusMessage = "Device disconnected";
                BluetoothLEHardwareInterface.DeInitialize(() =>
                {
                    _connected = false;
                    _state = States.None;
                });
            });
        }
        else
        {
            BluetoothLEHardwareInterface.DeInitialize(() =>
            {
                _state = States.None;
            });
        }
    }
}
