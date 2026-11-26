using Microsoft.Xna.Framework.Input;
using PristonTale.Gameplay.Screens.Opening;

namespace PristonTale.Tests;

internal static class OpeningIntroInputTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("opening intro touch skips only on Android release", TouchReleaseAsync);
    }

    private static Task TouchReleaseAsync()
    {
        Assert.True(OpeningScreen.IntroTouchReleased(true, true, ButtonState.Pressed, ButtonState.Released),
            "a completed Android tap must reveal login");
        Assert.True(!OpeningScreen.IntroTouchReleased(true, true, ButtonState.Released, ButtonState.Pressed),
            "a held finger must not activate login");
        Assert.True(!OpeningScreen.IntroTouchReleased(true, true, ButtonState.Pressed, ButtonState.Pressed),
            "a held finger must not repeatedly skip");
        Assert.True(!OpeningScreen.IntroTouchReleased(true, true, ButtonState.Released, ButtonState.Released),
            "idle input must not skip");
        Assert.True(!OpeningScreen.IntroTouchReleased(false, true, ButtonState.Pressed, ButtonState.Released),
            "desktop mouse behavior must remain unchanged");
        Assert.True(!OpeningScreen.IntroTouchReleased(true, false, ButtonState.Pressed, ButtonState.Released),
            "losing focus must not skip");
        return Task.CompletedTask;
    }
}
