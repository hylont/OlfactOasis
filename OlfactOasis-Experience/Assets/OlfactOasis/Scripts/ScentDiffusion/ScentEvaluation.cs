using System;
using System.Collections.Generic;
using UnityEngine;

// One diffusion of a scent at a given strength, and how the participant responded to it.
[Serializable]
public class ScentEvaluation
{
    public ScentDiffusionParameters Parameters;
    public EUserResponse WasPerceived;
    public EUserResponse WasPleasant;
    public List<Vector3> ResponseCurvePoints = new();
    public float ResponseMagnitude;

    public ScentEvaluation(ScentDiffusionParameters parameters)
    {
        Parameters = parameters;
    }

    // Copies the points: the drawing method reuses its own list and clears it as soon as the draw ends
    public void SetResponseCurve(List<Vector3> curvePoints)
    {
        ResponseCurvePoints = new List<Vector3>(curvePoints);
        ResponseMagnitude = GetResponseMagnitude(ResponseCurvePoints);
    }

    static float GetResponseMagnitude(List<Vector3> curvePointsList)
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
