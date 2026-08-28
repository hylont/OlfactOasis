public interface IScentDiffuser
{
    public bool RequestDiffusion(ScentDiffusionParameters p_params);
    public ScentDiffuserDeviceInfo GetDeviceStatus();
}
