public interface IScentDiffuser
{
    public bool RequestDiffusion(ScentDiffusionParameters parameters);
    public ScentDiffuserDeviceInfo GetDeviceStatus();
}
