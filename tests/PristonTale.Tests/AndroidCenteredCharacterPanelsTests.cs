using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.UI.Inventory;
using PristonTale.Gameplay.UI.Widgets;

namespace PristonTale.Tests;

internal static class AndroidCenteredCharacterPanelsTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android centered status and skills share window and input geometry", GeometryAsync);
    }

    private static Task GeometryAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(1280, 720), new Point(800, 600) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            var origin = HudInventoryLayout.CenteredPanelOrigin(layout);
            var window = HudInventoryLayout.Window(origin);
            Assert.True(window == HudStatusPanel.Window(origin), "status geometry differs from shared window");
            Assert.True(window == HudSkillTreeLayout.Window(origin), "skill geometry differs from shared window");
            Assert.True(Math.Abs(window.Center.X - layout.Width / 2) <= 1 && Math.Abs(window.Center.Y - layout.Height / 2) <= 1,
                "window not centered");
            Assert.True(window.Contains(HudStatusPanel.Close(origin)), "status close outside window");
            Assert.True(window.Contains(HudSkillTreeLayout.Close(origin)), "skill close outside window");
            for (var i = 0; i < 5; i++)
            {
                Assert.True(window.Contains(HudStatusPanel.AllocateButton(origin, i)), "attribute button outside window");
            }
            for (var i = 0; i < HudSkillTreeLayout.NodeCount; i++)
            {
                Assert.True(window.Contains(HudSkillTreeLayout.Node(origin, i)), "skill node outside window");
            }
            Assert.True(window.Contains(HudSkillTreeLayout.BasicAttack(origin)), "basic attack outside window");
            Assert.True(window.Contains(HudSkillTreeLayout.PageBack(origin)) && window.Contains(HudSkillTreeLayout.PageForward(origin)),
                "skill page arrows outside window");
            var screen = new Rectangle(0, 0, layout.Width, layout.Height);
            for (var i = 0; i < HudSkillBindings.ShortcutCount; i++)
            {
                var shortcut = MobileControls.SkillPanelShortcutBounds(layout, i);
                Assert.True(screen.Contains(shortcut) && shortcut.Bottom < window.Top, "shortcut overlaps skill window or leaves screen");
            }
        }
        return Task.CompletedTask;
    }
}
