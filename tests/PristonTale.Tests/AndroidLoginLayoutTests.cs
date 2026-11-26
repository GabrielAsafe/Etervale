using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;

namespace PristonTale.Tests;

internal static class AndroidLoginLayoutTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android login keyboard space and hit testing in both orientations", MobileAsync);
        yield return ("android login keyboard change preserves desktop layout", DesktopAsync);
    }

    private static Task MobileAsync()
    {
        Point[] sizes = [new(1080, 2400), new(2400, 1080), new(720, 1280), new(1280, 720), new(320, 240)];
        foreach (var size in sizes)
        {
            var layout = LoginLayout.Create(size.X, size.Y, true);
            Assert.True(layout.Compact && float.IsFinite(layout.Scale) && layout.Scale > 0, "mobile layout must be compact and valid");
            Assert.True(layout.Panel.Left * layout.Scale >= 0 && layout.Panel.Right * layout.Scale <= size.X,
                "mobile form must stay inside the drawable width");
            Assert.True(layout.Panel.Top * layout.Scale >= 0 && layout.Panel.Bottom * layout.Scale <= size.Y * 0.45f,
                "mobile form must leave the lower keyboard area free");
            foreach (var field in new[] { layout.Account, layout.Password, layout.Login })
            {
                Assert.True(layout.Panel.Contains(field), "input must stay inside the visible form");
                var point = new Point((int)(field.Center.X * layout.Scale), (int)(field.Center.Y * layout.Scale));
                Assert.True(field.Contains(layout.Pointer(point)), "rendered field center must hit the same field");
            }
            Assert.True(layout.Account.Bottom < layout.Password.Top && layout.Password.Bottom < layout.Login.Top,
                "login controls must not overlap");
        }
        return Task.CompletedTask;
    }

    private static Task DesktopAsync()
    {
        var layout = LoginLayout.Create(1920, 1080, false);
        Assert.True(!layout.Compact && layout.Scale == 1f && layout.Panel == new Rectangle(720, 180, 480, 720),
            "desktop panel geometry changed");
        Assert.True(layout.Account == new Rectangle(798, 547, 324, 42), "desktop account geometry changed");
        Assert.True(layout.Login == new Rectangle(798, 681, 324, 44), "desktop login geometry changed");
        return Task.CompletedTask;
    }
}
