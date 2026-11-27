using PristonTale.Engine3D.Rendering;

namespace PristonTale.Tests;

internal static class DepthPrepassPolicyTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("optional depth prepass preserves configured dependencies and legacy defaults", PolicyAsync);
    }

    private static Task PolicyAsync()
    {
        var defaults = new ClientSettings();
        Assert.True(defaults.DepthPrepass, "existing settings must keep the prepass");
        Assert.True(!defaults.HasSameRendering(defaults with { DepthPrepass = false }), "prepass setting change was ignored");
        Assert.True(defaults.HasSameDisplay(defaults with { DepthPrepass = false }), "prepass preference reset the display");
        for (var mask = 0; mask < 64; mask++)
        {
            bool Run(int samples, bool overdraw = false, bool shaded = true) =>
                PristonDepthPrepassPolicy.ShouldRun(samples, overdraw, shaded, (mask & 1) != 0, (mask & 2) != 0,
                    (mask & 4) != 0, (mask & 8) != 0, (mask & 16) != 0, (mask & 32) != 0);
            Assert.True(Run(0) == (mask != 0), $"wrong prepass policy for mask {mask}");
            Assert.True(!Run(2) && !Run(4) && !Run(0, overdraw: true), "MSAA or overdraw enabled the prepass");
            Assert.True(Run(0, shaded: false), "development view lost its prepass");
        }

        return Task.CompletedTask;
    }
}
