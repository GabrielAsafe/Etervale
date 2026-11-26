using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.UI.Inventory;
using PristonTale.Gameplay.UI.Widgets;

namespace PristonTale.Tests;

internal static class AndroidCenteredInventoryTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android centered inventory preserves original belt and window geometry", GeometryAsync);
    }

    private static Task GeometryAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(1280, 720) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            var origin = HudInventoryLayout.CenteredPanelOrigin(layout);
            var window = HudInventoryLayout.Window(origin);
            Assert.True(Math.Abs(window.Center.X - layout.Width / 2) <= 1, "inventory not horizontally centered");
            Assert.True(Math.Abs(window.Center.Y - layout.Height / 2) <= 1, "inventory not vertically centered");
            Assert.True(new Rectangle(0, 0, layout.Width, layout.Height).Contains(window), "inventory outside viewport");
            Assert.True(window.Contains(HudInventoryLayout.Bag(origin)), "bag outside inventory");
            Assert.True(window.Contains(HudInventoryLayout.Close(origin)), "close outside inventory");
            var central = new Rectangle(origin.X + HudBar.CentralLeft, origin.Y + HudBar.CentralTop, HudBar.CentralWidth, HudBar.CentralHeight);
            for (var index = 0; index < PristonTale.Simulation.Items.PotionBelt.Boxes; index++)
            {
                Assert.True(central.Contains(HudInventoryLayout.Belt(origin, index)), "belt outside central block");
            }
            var offset = 37;
            var sliding = HudInventoryLayout.CenteredPanelOrigin(layout, offset);
            Assert.True(sliding == origin + new Point(0, offset), "slide changed window alignment");
        }
        return Task.CompletedTask;
    }
}
