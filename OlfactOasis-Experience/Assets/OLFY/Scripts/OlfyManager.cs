using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
namespace Olfy
{
    public class OlfyManager : MonoBehaviour
    {
        public static OlfyManager Instance = null;
        #region public members
        [HideInInspector] public string address;
        [HideInInspector] public string batteryLevel;
        [HideInInspector] public string deviceInformation;
        [HideInInspector] public bool isReady = false;
        public ChoiceConnection choiceConnection;
        public GameObject bleManager;
        public enum ChoiceConnection
        {
            wifi,
            bluetooth,
        }
        #endregion

        #region private members
        private Dictionary<string, string> deviceDictionary;
        private int requestToSend = 0;
        private int sentRequest = 0;
        private bool once = false;
       
        #endregion

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
        //D�marrage du scan au lancement de l'application
        private void Start()
        {
            if (choiceConnection == ChoiceConnection.wifi)
                StartCoroutine(ScanNetwork());
            else
                bleManager.SetActive(true);
        }
        #region connexion methods
        IEnumerator ScanNetwork()
        {
          string addressStart = "";
            yield return null;
            deviceDictionary = new Dictionary<string, string>();

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ipAddress in host.AddressList)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    string[] temp = ipAddress.ToString().Split('.');
                    addressStart = temp[0] + "." + temp[1] + "." + temp[2] + ".";
                }
            }

            for (int i = 1; i <= 255; i++)
            {
                StartCoroutine(PingDevice(addressStart + i));
            }
        }

        IEnumerator PingDevice(string ip)
        {
            var ping = new Ping(ip);
            while (!ping.isDone)
            {
                yield return null;
            }
            Debug.Log("ip ping : " + ip);
            requestToSend++;
            Debug.Log(requestToSend);
            StartCoroutine(CheckOlfy(ip));
        }
        IEnumerator CheckOlfy(string ip)
        {
            var request = new UnityWebRequest("http://" + ip + "/olfy/status", "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            yield return request.SendWebRequest();
            Parser(request.downloadHandler.text, ip);
            sentRequest++;

        }

        public void Parser(string requestAnswer, string ip)
        {
            if (requestAnswer.Contains("DNSname"))
            {
                requestAnswer = requestAnswer.Replace("}", String.Empty);
                requestAnswer = requestAnswer.Replace("{", String.Empty);
                string[] properties = requestAnswer.Split(',');

                foreach (string property in properties)
                {
                    string[] keyValue = property.Split(':');
                    for (int i = 0; i < keyValue.Length; i++)
                    {
                        keyValue[i] = keyValue[i].Trim('"');
                    }

                    if (keyValue[0] == "DNSname")
                    {

                        deviceDictionary.Add(keyValue[1], ip);
                        Debug.Log("Success :    " + keyValue[1]);
                        if (!once)
                        {
                            address = ip;
                            StartCoroutine(GetBattery());
                            once = !once;
                            isReady = true;
                        }

                    }
                }
            }
        }
        #endregion

        #region communication methods
        //D�clenchement d'une odeur
        public void SendSmellToOlfy(int duration, string channel, int intensity, int freq, bool booster)
        {
            if (choiceConnection == ChoiceConnection.wifi)
                StartCoroutine(SmellCoroutine(duration, channel, intensity, freq, booster));
            else
                BleManager.Instance.Diffuse(duration, channel, intensity, freq, booster);
        }
        //D�clenchement d'une odeur
        private IEnumerator SmellCoroutine(int duration, string channel, int intensity, int freq, bool booster)
        {
            var request = new UnityWebRequest("http://" + address + "/olfy/diffuse", "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{\"duration\": " + duration + ", \"channel\": " + channel + ",\"intensity\": " + intensity + ",\"booster\": false}"); // Contenue du body avec les informations
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            Debug.Log("Status Code: " + request.responseCode);
        }
        //Mise en veille du dispositif
        private IEnumerator DeepSleep()
        {
            var request = new UnityWebRequest("http://" + address + "/olfy/deepsleep", "POST"); 
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            Debug.Log("Deep slepp Status Code: " + request.responseCode);
        }
        //Retourne les informations du Firmware
        private IEnumerator GetFirmwareInformation()
        {
            var request = new UnityWebRequest("http://" + address + "/olfy/firmware", "GET");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{\"channel\": " + /*channel*/1 + ",\"duration\": " + /*duration */6000 + "}");
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json"); 

            yield return request.SendWebRequest(); 
            deviceInformation = request.responseCode.ToString();
            Debug.Log("Firmware: " + request.responseCode); 
        }
        //Retourne le pourcentage de la batterie
        public IEnumerator GetBattery()
        {
            if (choiceConnection == ChoiceConnection.wifi)
            {
                var request = new UnityWebRequest("http://" + address + "/olfy/getbatt", "GET");

                request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

                yield return request.SendWebRequest();
                string data = JObject.Parse(request.downloadHandler.text)["battery"].ToString();
                batteryLevel = data;
                Debug.Log(request.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning("Non disponible pour le moment");
            }
        }
        //Arr�te l'ensemble des diffusions
        private IEnumerator StopOlfy()
        {
            var request = new UnityWebRequest("http://" + address + "/olfy/stop_all", "POST"); 
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{}"); 
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json"); 

            yield return request.SendWebRequest(); 

            Debug.Log("status: " + request.responseCode); 
        }
        #endregion

    }





}
