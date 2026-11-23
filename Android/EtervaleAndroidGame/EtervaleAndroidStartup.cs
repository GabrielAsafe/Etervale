// ETERVALE_ANDROID_V5: writable Android config, seeded only if absent.
using System;
using System.IO;
using System.Text.Json;
using Android.App;
using Android.Util;
using PristonTale.Engine3D.Rendering;

namespace EtervaleAndroidGame;

internal static class EtervaleAndroidStartup
{
    public static string PrepareSettings(Activity activity)
    {
        string root = activity.GetExternalFilesDir(null)?.AbsolutePath
            ?? activity.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Sem armazenamento privado Android.");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "client-settings.json");
        if (File.Exists(path))
        {
            Log.Info("ETERVALE_3D", "Definicoes Android existentes preservadas: " + path);
            return path;
        }

        // Low is appropriate for the OPPO Mali-G57, and avoids the desktop's
        // four high-resolution cascaded shadow maps, PCSS, AO and expensive TAA.
        var settings = new ClientSettings
        {
            ServerAddress = "tcp://192.168.1.111:10009?cert=89CCCD31A09D1218460E0610B7F03EDB3D4E3EFA35D4A721DFEAE74CC77379D7",
            Language = "pt-BR",
            WindowWidth = 1280,
            WindowHeight = 720,
            MultiSampleCount = 0,
            MaxFps = 60,
            BackgroundMaxFps = 30,
            VerticalSync = true
        }.WithQuality(GraphicsQuality.Low);

        File.WriteAllText(path, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Log.Info("ETERVALE_3D", "Definicoes Android criadas: " + path + " quality=Low");
        return path;
    }
}
