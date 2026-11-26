using System.Diagnostics;

namespace PristonTale.Engine3D.Diagnostics;

// The Android host provides the platform log sink; other clients never need an Android dependency.
public static class MobileDiagnosticTrace
{
    private static Action<string, string> _sink;
    private static long _startedAt;

    public static void Start(Action<string, string> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _startedAt = Stopwatch.GetTimestamp();
        _sink = sink;
        Mark("activity_on_create");
    }

    public static void Mark(string stage)
    {
        var sink = _sink;
        if (sink is null)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;
        sink("ETERVALE_BOOT", FormattableString.Invariant($"stage={stage} elapsed_ms={elapsed:F1}"));
    }

    public static void WritePerformance(string message) => _sink?.Invoke("ETERVALE_PERF", message);

    internal static void Clear() => _sink = null;
}
