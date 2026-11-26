namespace PristonTale.Engine3D.Rendering;

public static class PristonDepthPrepassPolicy
{
    // The opt-out is deliberately conservative: configured depth consumers keep their input even when
    // their actual work is conditional later in the frame. Preserve TAA and development views as well.
    public static bool ShouldRun(int multiSampleCount, bool overdraw, bool shaded, bool temporal,
        bool preferred, bool ambientOcclusion, bool contactShadows, bool screenReflections, bool volumetricLight)
    {
        return multiSampleCount == 0 && !overdraw && (preferred || !shaded || temporal || ambientOcclusion
            || contactShadows || screenReflections || volumetricLight);
    }
}
