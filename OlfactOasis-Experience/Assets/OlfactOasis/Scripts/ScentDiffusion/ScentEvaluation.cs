using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ScentEvaluation
{
    public ScentDiffusionParameters Parameters;
    public bool WasPerceived = false;
    public EUserResponse WasPleasant;
    public List<Vector3> ResponseCurvePoints;
    public float ResponseMagnitude;

    public ScentEvaluation(ScentDiffusionParameters parameters, bool wasPerceived, EUserResponse wasPleasant, List<Vector3> responseCurvePoints)
    {
        Parameters = parameters;
        WasPerceived = wasPerceived;
        WasPleasant = wasPleasant;
        ResponseCurvePoints = responseCurvePoints;
        ResponseMagnitude = GetResponseMagnitude(ResponseCurvePoints);
    }

    public static float GetResponseMagnitude(List<Vector3> curvePointsList)
    {
        if(curvePointsList.Count < 2)
        {
            LLogger.W("Can't determine curve length using a single point.");
            return 0f;
        }

        float totalDistance = 0;
        for(int idxPoint = 0; idxPoint < curvePointsList.Count - 1; idxPoint++)
        {
            totalDistance += Vector3.Distance(curvePointsList[idxPoint], curvePointsList[idxPoint + 1]);
        }
        return totalDistance;
    }
}