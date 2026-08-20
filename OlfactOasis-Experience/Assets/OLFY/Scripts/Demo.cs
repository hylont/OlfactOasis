using System.Collections;
using UnityEngine;
using Olfy;

public class Demo : MonoBehaviour
{
    public GameObject connectionEstablished;
    public GameObject waitingForConnection;
    public GameObject buses;
    public TMPro.TextMeshProUGUI ipText;
    public TMPro.TextMeshProUGUI batteryText;
    public TMPro.TextMeshProUGUI intensityText;
    public TMPro.TextMeshProUGUI durationText;
    public TMPro.TextMeshProUGUI frequencyText;

    //Valeurs par défaut
    private int intensityValue = 50;
    private float durationValue = 2f;
    private int frequencyValue = 110000;

    void Update()
    {
        //Attente de la connexion pour afficher les buses
        if(OlfyManager.Instance.isReady && connectionEstablished.activeInHierarchy == false)
        {
            connectionEstablished.SetActive(true);
            buses.SetActive(true);
            waitingForConnection.SetActive(false);
            if (OlfyManager.Instance.choiceConnection == OlfyManager.ChoiceConnection.wifi)
            {
                ipText.text = "IP : " + OlfyManager.Instance.address;
                StartCoroutine(WaitingForBatteryLevel());
            }
            else
            {
                ipText.gameObject.SetActive(false);
            }
        }
    }
    IEnumerator WaitingForBatteryLevel()
    {
        yield return new WaitForSeconds(2f);
        batteryText.text = OlfyManager.Instance.batteryLevel;
    }
    //Fonction d'envoie vers le dispositif (Déclenchée depuis les boutons buses dans la scène demo)
    public void SendToOlfy(string buse)
    {
        OlfyManager.Instance.SendSmellToOlfy((int)durationValue * 1000, buse, intensityValue, frequencyValue, false);
    }
    //Fonction de changement d'intensité gérée par un des sliders de la scène démo
    public void SetIntensity(float i)
    {
        intensityValue = (int)i;
        intensityText.text = i.ToString();
    }
    //Fonction de changement de la durée de diffusion gérée par un des sliders de la scène démo
    public void SetDuration(float d)
    {
        durationValue = d;
        float dur = Mathf.Round(durationValue);
        durationText.text = dur.ToString();
    }
    //Fonction de changement de fréquence gérée par un des sliders de la scène démo
    public void SetFrequency(float f)
    {
        frequencyValue = (int)f;
        int freq = (int)f / 1000;
        frequencyText.text = freq.ToString() + " k";
    }

}
