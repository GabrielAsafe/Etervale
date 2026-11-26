using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.World;

namespace PristonTale.Tests;

internal static class AndroidLootInputTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android loot control takes input over minimap after grid drag", OverlapAsync);
        yield return ("android explicit loot respects scene locks", EligibilityAsync);
    }

    private static Task OverlapAsync()
    {
        var layout = HudLayout.Create(800, 600, mobile: true);
        var controls = new MobileControls();
        var map = controls.MapBounds(layout);
        var target = map.Center;
        Assert.True(controls.Hit(layout, target, true, false) == MobileControls.Role.None, "uncovered minimap accepted a world touch");
        var loot = MobileControls.Bounds(layout, MobileControls.Role.Loot);
        var snapped = MobileControls.SnapHudCenter(layout, loot, target.ToVector2());
        Assert.True(map.Contains(snapped), "test did not create minimap overlap");
        controls.MoveHudControl(layout, MobileControls.Role.Loot, target.ToVector2());
        Assert.True(controls.Hit(layout, snapped, true, false) == MobileControls.Role.Loot, "minimap swallowed the moved loot control");
        return Task.CompletedTask;
    }

    private static Task EligibilityAsync()
    {
        Assert.True(GameScene.CanProcessMobileLoot(true, false, false, true), "explicit loot was blocked in the active scene");
        Assert.True(!GameScene.CanProcessMobileLoot(false, false, false, true), "loot ran without a request");
        Assert.True(!GameScene.CanProcessMobileLoot(true, true, false, true), "loot bypassed movement lock");
        Assert.True(!GameScene.CanProcessMobileLoot(true, false, true, true), "loot bypassed keyboard capture");
        Assert.True(!GameScene.CanProcessMobileLoot(true, false, false, false), "loot bypassed inactive camera input");
        return Task.CompletedTask;
    }
}
