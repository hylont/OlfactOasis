using System.Collections.Generic;

public class ScentDiffuserDeviceInfo
{
    public EScentDiffuserStatus Status = EScentDiffuserStatus.UNKNOWN;
    public int BatteryLevel = 0;
    public Dictionary<int, EScentSlotStatus> SlotsStatuses = new();
}
