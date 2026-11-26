using System.Diagnostics;
using PristonTale.Engine3D.Diagnostics;

namespace PristonTale.Tests;

internal static class RecentFrameTimingTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("recent frame sample excludes old slow frames after ring wraps", RecentAsync);
    }

    private static Task RecentAsync()
    {
        var timing = new FrameTiming();
        long now = Stopwatch.Frequency;
        timing.RecordDraw(now, now);
        for (var i = 0; i < 400; i++)
        {
            var step = Stopwatch.Frequency / (i < 370 ? 10 : 50);
            now += step;
            timing.RecordUpdate(step / 4, i < 370 ? 100 : 200);
            timing.RecordDraw(now - step / 4, now, i < 370 ? 100 : 200);
            timing.RecordPresentation(step / 2, 100);
        }
        Assert.True(timing.TrySample(now, out var recent, 30), "recent sample missing");
        Assert.True(Math.Abs(recent.FramesPerSecond - 50) < 0.01, "old frames contaminated recent FPS");
        Assert.True(Math.Abs(recent.FrameP95Ms - 20) < 0.01, "incorrect recent percentile");
        Assert.True(Math.Abs(recent.PresentP95Ms - 10) < 0.01, "incorrect presentation percentile");
        Assert.True(recent.AllocatedBytesPerFrame == 500, "incorrect recent allocation average");
        Assert.True(timing.TrySample(now + Stopwatch.Frequency, out var full), "full sample missing");
        Assert.True(full.FramesPerSecond < 15, "desktop full history was discarded");
        return Task.CompletedTask;
    }
}
