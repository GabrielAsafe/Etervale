using System.Globalization;
using PristonTale.Engine3D.Diagnostics;

namespace PristonTale.Tests;

internal static class MobileDiagnosticTraceTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("mobile startup and performance use distinct platform log tags", TraceAsync);
    }

    private static Task TraceAsync()
    {
        var messages = new List<(string Tag, string Message)>();
        try
        {
            MobileDiagnosticTrace.Start((tag, message) => messages.Add((tag, message)));
            MobileDiagnosticTrace.Mark("login_first_draw");
            MobileDiagnosticTrace.WritePerformance("FPS 7");
            Assert.True(messages.Count == 3, "trace message missing");
            Assert.True(messages[0].Tag == "ETERVALE_BOOT" && messages[0].Message.StartsWith("stage=activity_on_create "), "start not logged");
            Assert.True(messages[1].Tag == "ETERVALE_BOOT" && messages[1].Message.StartsWith("stage=login_first_draw "), "login mark missing");
            var start = double.Parse(messages[0].Message.Split('=')[2], CultureInfo.InvariantCulture);
            var login = double.Parse(messages[1].Message.Split('=')[2], CultureInfo.InvariantCulture);
            Assert.True(start >= 0 && login >= start, "non-monotonic startup marks");
            Assert.True(messages[2] == ("ETERVALE_PERF", "FPS 7"), "performance tag or content changed");
        }
        finally
        {
            MobileDiagnosticTrace.Clear();
        }
        MobileDiagnosticTrace.Mark("disabled");
        MobileDiagnosticTrace.WritePerformance("disabled");
        Assert.True(messages.Count == 3, "unconfigured sink still wrote logs");
        return Task.CompletedTask;
    }
}
