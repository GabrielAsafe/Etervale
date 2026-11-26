using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.UI.Inventory;
using PristonTale.Gameplay.UI.Widgets;

namespace PristonTale.Tests;

internal static class AndroidClassicCentralHudTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android central hud contains the original potion slots and sockets", GeometryAsync);
    }

    private static Task GeometryAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(1280, 720) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            var bounds = HudBar.MobileCentralBounds(layout);
            var origin = HudBar.CentralOrigin(bounds);
            Assert.True(new Rectangle(0, 0, layout.Width, layout.Height).Contains(bounds), "central HUD is outside viewport");
            Assert.True(Math.Abs(bounds.Center.X - layout.Width / 2) <= 1, "central HUD is not centered");
            for (var box = 0; box < PristonTale.Simulation.Items.PotionBelt.Boxes; box++)
            {
                var slot = HudInventoryLayout.Belt(origin, box);
                Assert.True(bounds.Contains(slot), "potion slot outside central HUD");
            }
            foreach (var right in new[] { false, true })
            {
                var socket = HudBar.OffsetSocket(right, origin.X);
                socket.Offset(0, origin.Y);
                Assert.True(bounds.Contains(socket), "skill socket outside central HUD");
            }
            for (var role = (int)MobileControls.Role.SkillF1; role <= (int)MobileControls.Role.SkillF8; role++)
            {
                Assert.True(!bounds.Intersects(MobileControls.Bounds(layout, (MobileControls.Role)role)), "default central HUD overlaps a shortcut");
            }
        }
        return Task.CompletedTask;
    }
}
