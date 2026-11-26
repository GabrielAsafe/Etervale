using Microsoft.Xna.Framework;
using PristonTale.Gameplay.Screens.Framework;

namespace PristonTale.Tests;

internal static class AndroidDiagnosticsLayoutTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("android metrics text is centred and fits both orientations", FitAsync);
    }

    private static Task FitAsync()
    {
        foreach (var viewport in new[] { new Point(2400, 1080), new Point(1080, 2400), new Point(800, 600) })
        {
            foreach (var size in new[] { new Vector2(300, 200), new Vector2(2200, 700), new Vector2(300, 3000) })
            {
                var (position, scale) = GameClient.MobileDiagnosticsPlacement(viewport.X, viewport.Y, size);
                var drawn = size * scale;
                Assert.True(scale > 0f && scale <= 1f, "invalid text scale");
                Assert.Near(viewport.X / 2f, position.X + drawn.X / 2f, 0.01f);
                Assert.Near(viewport.Y / 2f, position.Y + drawn.Y / 2f, 0.01f);
                Assert.True(position.X >= viewport.X * 0.059f && position.Y >= viewport.Y * 0.059f, "text exceeds safe margins");
            }
        }
        return Task.CompletedTask;
    }
}
