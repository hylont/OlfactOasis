using System;
using System.Collections.Generic;

[Serializable]
public class ScentData
{
    public EScentName Name = EScentName.UNKNOWN;
    public int SlotIndex = 1;
    public int DefaultVibrationFrequency = 11000;
    public List<ScentEvaluation> Evaluations = new();
}
