// Carries the calibration outcome across the scene switch from ScentCalibrationScene to StoreScene
// (mirrors ParticipantData's static-holder pattern, used the same way for the same reason).
public static class CalibrationResultHolder
{
    public static EScentName ChosenScent = EScentName.Unknown;
    public static ScentDiffusionParameters OptimalParameters;
}
