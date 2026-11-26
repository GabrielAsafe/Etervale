using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;

namespace PristonTale.Tests;

internal static class AndroidDiagnosticsInputTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android metrics menu routes one diagnostic request per tap", RequestAsync);
        yield return ("android metrics and system menu buttons have separate hit areas", GeometryAsync);
    }

    private static Task RequestAsync()
    {
        var layout = HudLayout.Create(2400, 1080, mobile: true);
        var controls = new MobileControls();
        Assert.True(!controls.TakeDiagnosticsRequest(), "unexpected initial request");
        controls.OnPress(MobileControls.Role.Menu);
        var bounds = MobileControls.Bounds(layout, MobileControls.Role.Diagnostics);
        var role = controls.Hit(layout, bounds.Center, true, false);
        Assert.True(role == MobileControls.Role.Diagnostics, "metrics menu did not receive touch");
        controls.OnPress(role);
        Assert.True(controls.TakeDiagnosticsRequest(), "metrics did not request overlay toggle");
        Assert.True(!controls.TakeDiagnosticsRequest(), "request repeated without a new tap");
        controls.OnPress(MobileControls.Role.Menu);
        controls.OnPress(MobileControls.Role.Diagnostics);
        controls.Reset();
        Assert.True(!controls.TakeDiagnosticsRequest(), "reset left a stale metrics request");
        return Task.CompletedTask;
    }

    private static Task GeometryAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(800, 600) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            var controls = new MobileControls();
            var screen = new Rectangle(0, 0, layout.Width, layout.Height);
            var system = MobileControls.Bounds(layout, MobileControls.Role.System);
            var metrics = MobileControls.Bounds(layout, MobileControls.Role.Diagnostics);
            var edit = MobileControls.Bounds(layout, MobileControls.Role.Edit);
            Assert.True(screen.Contains(metrics) && !metrics.Intersects(system) && !metrics.Intersects(edit), "metrics overlaps another menu button");
            controls.OnPress(MobileControls.Role.Menu);
            Assert.True(controls.Hit(layout, metrics.Center, true, false) == MobileControls.Role.Diagnostics, "metrics hit area differs from drawing");
            Assert.True(controls.Hit(layout, system.Center, true, false) == MobileControls.Role.System, "system hit area differs from drawing");
        }
        return Task.CompletedTask;
    }
}
