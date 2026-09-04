using UnityEngine;

public class ParticipantData
{
    private static string _ID = string.Empty;

    private static bool _generateID = true;

    public static void SetID(string id)
    {
        _ID = id;
    }

    public static string GetID()
    {
        if (_ID == string.Empty)
        {
            LLogger.E("Participant ID has to be set first.");
            if (_generateID)
            {
                _ID = System.Guid.NewGuid().ToString();
                LLogger.L($"Generated new participant ID: {_ID}");
            }
            else
            {
                return null;
            }
        }

        return _ID;
    }
}
