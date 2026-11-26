#if DEBUG
using PristonTale.Gameplay.Screens.Framework;

namespace PristonTale.Tests;

internal static class AndroidRegressionSessionTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("debug regression credentials and character are consumed once", OneShotAsync);
    }

    private static Task OneShotAsync()
    {
        try
        {
            Assert.True(!AndroidRegressionSession.Configure(["account", "bad password", "Test"]), "invalid input accepted");
            Assert.True(AndroidRegressionSession.Configure(["test.account", "test-pass", "Test"]), "valid request rejected");
            Assert.True(AndroidRegressionSession.HasLogin, "login missing");
            Assert.True(!AndroidRegressionSession.Configure(["test", "test", "Test", "invalid"]), "unknown scenario accepted");
            Assert.True(AndroidRegressionSession.Configure(["test.account", "test-pass", "Test", "graphics"]), "graphics scenario rejected");
            Assert.True(AndroidRegressionSession.Scenario == "graphics", "wrong scenario");
            Assert.True(AndroidRegressionSession.TakeLogin()?.Account == "test.account", "wrong login");
            Assert.True(AndroidRegressionSession.TakeLogin() is null, "login reused");
            Assert.True(AndroidRegressionSession.TakeCharacter() == "Test", "wrong character");
            Assert.True(AndroidRegressionSession.TakeCharacter() is null, "character reused");
            Assert.True(AndroidRegressionSession.Configure(["test", "test", "Test"]), "second request rejected");
            AndroidRegressionSession.CancelSelection();
            Assert.True(AndroidRegressionSession.TakeCharacter() is null, "cancelled selection reused");
        }
        finally
        {
            AndroidRegressionSession.Configure([]);
        }

        return Task.CompletedTask;
    }
}
#endif
