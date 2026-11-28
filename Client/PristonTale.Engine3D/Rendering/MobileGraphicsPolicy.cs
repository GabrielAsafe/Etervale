using System.Text.Json.Serialization;
using PristonTale.Engine3D.Rendering.Lighting;

namespace PristonTale.Engine3D.Rendering;

[JsonConverter(typeof(JsonStringEnumConverter<MobileResolution>))]
public enum MobileResolution
{
    High,
    Medium,
    Low
}

public static class MobileGraphicsPolicy
{
    public static int ScalePercent(MobileResolution resolution) => resolution switch
    {
        MobileResolution.Medium => 75,
        MobileResolution.Low => 50,
        _ => 100
    };

    public static MobileResolution NextResolution(MobileResolution resolution, bool backwards)
    {
        var index = Enum.IsDefined(resolution) ? (int)resolution : 0;
        var count = (int)MobileResolution.Low + 1;
        return (MobileResolution)((index + (backwards ? -1 : 1) + count) % count);
    }

    public static ClientSettings Apply(ClientSettings settings) => settings with
    {
        MobileResolution = Enum.IsDefined(settings.MobileResolution) ? settings.MobileResolution : MobileResolution.High,
        Quality = GraphicsQuality.Custom,
        RealisticSky = settings.MobileRealisticSky,
        ShadowsEnabled = settings.MobileRealisticSky,
        CloudQuality = CloudQuality.Low,
        ShadowMapSize = 1024,
        ShadowCascades = 2,
        ShadowFilter = ShadowFilterMode.Pcf,
        ShadowUpdateRate = 30,
        LampShadowCount = 0,
        AntiAliasing = AntiAliasingMode.Off,
        MultiSampleCount = 0,
        AmbientOcclusion = false,
        ContactShadows = false,
        DepthPrepass = false,
        ImageBasedLighting = false,
        ScreenSpaceReflections = false,
        VolumetricLight = false,
        CalibrationSpheres = false,
        DebugView = PristonDebugView.Shaded,
        Bloom = BloomQuality.Off,
        AutoExposure = false,
        ExposureCompensation = 0f,
        Tonemapper = TonemapperMode.PbrNeutral,
        ColorGrading = false,
        Sharpening = 0f,
        LocalLightLimit = 8,
        MaxFps = 60,
        BackgroundMaxFps = 30,
        VerticalSync = false
    };
}
