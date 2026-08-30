using System;

public partial class OlfyHandler
{
    [Serializable]
    public class ScentSlotData
    {
        public EScentSlotStatus Status = EScentSlotStatus.Unknown;

        public ScentSlotData(EScentSlotStatus status)
        {
            this.Status = status;
        }
    }
}
