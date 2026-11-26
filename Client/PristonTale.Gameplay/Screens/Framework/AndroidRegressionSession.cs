#if DEBUG
#nullable enable
using PristonTale.Engine3D.Diagnostics;

namespace PristonTale.Gameplay.Screens.Framework;

// One-shot debug request, populated by the Android host before the game loop starts.
public static class AndroidRegressionSession
{
    private static (string Account, string Password)? _login;
    private static string? _character;
    public static bool Active { get; private set; }
    public static string Scenario { get; private set; } = "rotation";
    public static bool GameplayReady { get; private set; }
    public static void SetGameplayReady() => GameplayReady = Active;

    internal static bool HasLogin => _login.HasValue;

    public static void LoadOnce(string privateFilesDirectory)
    {
        var path = Path.Combine(privateFilesDirectory, ".etervale-e2e-request");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string[] lines;
            try
            {
                lines = new FileInfo(path).Length <= 4096 ? File.ReadAllLines(path) : [];
            }
            finally
            {
                File.Delete(path);
            }

            if (!Configure(lines))
            {
                MobileDiagnosticTrace.Mark("regression_failed_invalid_request");
                return;
            }

            Mark("request_loaded");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MobileDiagnosticTrace.Mark("regression_failed_request_io");
        }
    }

    internal static bool Configure(string[] lines)
    {
        Active = false;
        Scenario = "rotation";
        GameplayReady = false;
        _login = null;
        _character = null;
        if (lines.Length is not (3 or 4) || !ValidCredential(lines[0]) || !ValidCredential(lines[1])
            || string.IsNullOrWhiteSpace(lines[2]) || lines[2].Length > 128)
        {
            return false;
        }

        if (lines.Length == 4)
        {
            if (lines[3] is not ("rotation" or "graphics" or "pacing" or "performance"))
            {
                return false;
            }
            Scenario = lines[3];
        }
        _login = (lines[0], lines[1]);
        _character = lines[2];
        Active = true;
        return true;
    }

    private static bool ValidCredential(string value)
    {
        if (value.Length is < 1 or > 20)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    internal static (string Account, string Password)? TakeLogin()
    {
        var value = _login;
        _login = null;
        return value;
    }

    internal static string? TakeCharacter()
    {
        var value = _character;
        _character = null;
        return value;
    }

    internal static void CancelSelection() => _character = null;

    internal static void Mark(string stage)
    {
        if (Active)
        {
            MobileDiagnosticTrace.Mark("regression_" + stage);
        }
    }
}
#endif
