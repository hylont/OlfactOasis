using System;
using System.Collections.Generic;

[Serializable]
public class ScentData
{
    public EScentName Name = EScentName.Unknown;
    public int SlotIndex = 1;
    public int DefaultVibrationFrequency = 11000;
    public List<ScentEvaluation> Evaluations = new();
    public ScentDiffusionParameters MildParameters, OptimalParameters, StrongParameters;

    public bool GetValidity()
    {
        return new Random().Next(0,1)==1; //TODO LATER
    }
}
