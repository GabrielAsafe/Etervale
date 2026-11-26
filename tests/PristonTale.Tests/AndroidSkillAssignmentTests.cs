using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.UI.Widgets;
using PristonTale.Simulation.Characters;
using PristonTale.Simulation.Skills;

namespace PristonTale.Tests;

internal static class AndroidSkillAssignmentTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android skill touch selects a compatible socket and assigns a shortcut", SelectAndAssignAsync);
        yield return ("android skill assignment row fits above the skill window", RowAsync);
    }

    private static Task SelectAndAssignAsync()
    {
        const uint rightId = 0x101;
        const uint leftId = 0x102;
        const uint passiveId = 0x103;
        Skill[] skills =
        [
            new() { Code = rightId, Name = "Right", Tier = JobTier.Job1, Category = SkillCategory.ActiveAttack, MouseUse = SkillMouseUse.Right },
            new() { Code = leftId, Name = "Left", Tier = JobTier.Job1, Category = SkillCategory.ActiveAttack, MouseUse = SkillMouseUse.Left },
            new() { Code = passiveId, Name = "Passive", Tier = JobTier.Job1, Category = SkillCategory.Passive, MouseUse = SkillMouseUse.None }
        ];
        var bindings = new HudSkillBindings();
        bindings.SyncLearned(skills, [new(rightId, 1), new(leftId, 1), new(passiveId, 1)]);
        Assert.True(!bindings.TryAssignMobileShortcut(0), "assignment without selection");
        var down = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        bindings.Update(default, down, rightId, mobileSkillTouch: true);
        bindings.Update(default, default, rightId, mobileSkillTouch: true);
        Assert.True(bindings.RightSkillId == rightId, "touch did not select right-only skill");
        Assert.True(bindings.TryAssignMobileShortcut(0), "F1 assignment failed");
        bindings.Update(default, down, leftId, mobileSkillTouch: true);
        bindings.Update(default, default, leftId, mobileSkillTouch: true);
        Assert.True(bindings.LeftSkillId == leftId, "touch did not select left-only skill");
        Assert.True(bindings.TryAssignMobileShortcut(1), "F2 assignment failed");
        Assert.True(bindings.Shortcut(0) == new HudSkillShortcut(rightId, SkillMouseUse.Right), "wrong F1 binding");
        Assert.True(bindings.Shortcut(1) == new HudSkillShortcut(leftId, SkillMouseUse.Left), "wrong F2 binding");
        bindings.Update(default, down, passiveId, mobileSkillTouch: true);
        bindings.Update(default, default, passiveId, mobileSkillTouch: true);
        Assert.True(!bindings.TryAssignMobileShortcut(2), "passive skill or stale choice assigned");
        bindings.Update(new KeyboardState(Keys.F1), default, 0);
        Assert.True(bindings.RightSkillId == rightId, "F1 did not recall right socket");
        return Task.CompletedTask;
    }

    private static Task RowAsync()
    {
        foreach (var size in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(1280, 720) })
        {
            var layout = HudLayout.Create(size.X, size.Y, mobile: true);
            var screen = new Rectangle(0, 0, layout.Width, layout.Height);
            var origin = PristonTale.Gameplay.UI.Inventory.HudInventoryLayout.CenteredPanelOrigin(layout);
            var window = HudSkillTreeLayout.Window(origin);
            for (var i = 0; i < HudSkillBindings.ShortcutCount; i++)
            {
                var rect = MobileControls.SkillPanelShortcutBounds(layout, i);
                Assert.True(screen.Contains(rect) && rect.Bottom <= window.Top && !rect.Intersects(window), "shortcut outside the space above skills");
                if (i > 0)
                {
                    Assert.True(!rect.Intersects(MobileControls.SkillPanelShortcutBounds(layout, i - 1)), "overlapping shortcuts");
                }
            }
        }
        return Task.CompletedTask;
    }
}
