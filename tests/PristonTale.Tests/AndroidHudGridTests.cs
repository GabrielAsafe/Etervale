using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;

namespace PristonTale.Tests;

internal static class AndroidHudGridTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android HUD grid snaps and preserves saved centers", SnapAsync);
        yield return ("android HUD grid handles odd sizes and narrow layouts", NarrowAsync);
    }

    private static Task SnapAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(1280, 720), new Point(800, 600) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            foreach (var role in new[] { MobileControls.Role.Vitals, MobileControls.Role.Minimap, MobileControls.Role.Stick,
                         MobileControls.Role.Potion1, MobileControls.Role.SkillF1, MobileControls.Role.SkillF8, MobileControls.Role.Toggle })
            {
                foreach (var scale in new[] { 80, 100, 120 })
                {
                    var bounds = MobileControls.Bounds(layout, role, scale);
                    foreach (var target in new[] { new Vector2(-100, -100), new Vector2(layout.Width + 100, layout.Height + 100), new Vector2(231, 321) })
                    {
                        var point = MobileControls.SnapHudCenter(layout, bounds, target);
                        Assert.True(point.X % MobileControls.HudGridStep == 0 && point.Y % MobileControls.HudGridStep == 0, "center off grid");
                        var normalized = new Vector2(point.X / (float)layout.Width, point.Y / (float)layout.Height);
                        var restored = MobileControls.SavedHudBounds(layout, bounds, normalized);
                        Assert.True(restored.Center == point, "saved center rounded off grid");
                        Assert.True(restored.Left >= MobileControls.HudGridHorizontalMargin && restored.Right <= layout.Width - MobileControls.HudGridHorizontalMargin,
                            "horizontal safety margin crossed");
                        Assert.True(restored.Top >= MobileControls.HudGridVerticalMargin && restored.Bottom <= layout.Height - MobileControls.HudGridVerticalMargin,
                            "vertical safety margin crossed");
                    }
                }
            }
            var old = MobileControls.SavedHudBounds(layout, new Rectangle(0, 0, 52, 40), new Vector2(0.253f, 0.367f));
            Assert.True(old.Center.X == (int)MathF.Round(0.253f * layout.Width) && old.Center.Y == (int)MathF.Round(0.367f * layout.Height), "old layout resnapped");
        }
        return Task.CompletedTask;
    }

    private static Task NarrowAsync()
    {
        var layout = new HudLayout(1, 105, 71, 105, 71);
        var bounds = new Rectangle(0, 0, 99, 65);
        var point = MobileControls.SnapHudCenter(layout, bounds, new Vector2(999, -999));
        var restored = MobileControls.SavedHudBounds(layout, bounds, new Vector2(point.X / (float)layout.Width, point.Y / (float)layout.Height));
        Assert.True(restored.Center == point && new Rectangle(0, 0, layout.Width, layout.Height).Contains(restored), "narrow layout clipped the control");
        return Task.CompletedTask;
    }
}
