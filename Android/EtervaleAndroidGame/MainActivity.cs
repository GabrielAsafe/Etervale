using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Widget;
using PristonTale.Engine3D.Diagnostics;

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]

namespace EtervaleAndroidGame;

[Activity(Label = "Etervale 3D", MainLauncher = true, Exported = true,
    ScreenOrientation = ScreenOrientation.Unspecified)]
public sealed class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        MobileDiagnosticTrace.Start(static (tag, message) => Android.Util.Log.Info(tag, message));
        base.OnCreate(savedInstanceState);

        var column = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        int p = (int)(20 * Resources!.DisplayMetrics!.Density);
        column.SetPadding(p, p, p, p);

        var title = new TextView(this)
        {
            Text = "ETERVALE — CLIENTE 3D",
            TextSize = 24
        };
        var status = new TextView(this)
        {
            Text = "Cliente 3D gerido integrado. Ainda falta portar o runtime nativo SDL2/Vulkan para Android.",
            TextSize = 16
        };
        var start = new Button(this) { Text = "Tentar arrancar o jogo" };

        column.AddView(title);
        column.AddView(start);
        column.AddView(status);
        SetContentView(column);
        MobileDiagnosticTrace.Mark("launcher_view_set");

        // ETERVALE_ANDROID_V6_AUTO_START
        // Arrancar automaticamente, sem exigir um toque no botao do bootstrap.
        start.Post(() =>
        {
            start.PerformClick();
            start.Visibility = Android.Views.ViewStates.Gone;
        });
        start.Click += async (_, _) =>
        {
            start.Enabled = false;
            MobileDiagnosticTrace.Mark("auto_start_clicked");
            status.Text = "A iniciar Game1 (Etervale 3D)...";
            try
            {
                // O game loop SDL/Vulkan deve correr numa thread propria.
                // ETERVALE_ANDROID_CONTENT_V3
                EtervaleAndroidContentProvider.Configure(this);
                MobileDiagnosticTrace.Mark("content_provider_configured");
                // ETERVALE_ANDROID_V5
                PristonTale.Gameplay.UI.LoginPanel.AndroidKeyboardRequested = EtervaleAndroidSoftKeyboard.SetVisible;
                PristonTale.Gameplay.UI.Chat.HudChat.AndroidKeyboardRequested = EtervaleAndroidSoftKeyboard.SetVisible;
                string androidSettingsPath = EtervaleAndroidStartup.PrepareSettings(this);
                MobileDiagnosticTrace.Mark("settings_prepared");
#if DEBUG
                PristonTale.Gameplay.Screens.Framework.AndroidRegressionSession.LoadOnce(FilesDir!.AbsolutePath);
#endif
                // ETERVALE_ANDROID_SDL_BRIDGE_V2
                // Abre a Activity SDL que cria SurfaceView, contexto JNI e janela nativa.
                // Espera inicializacao da Activity antes de entrar no Game1.Run().
                var intent = new Intent();
                intent.SetClassName(PackageName!, "org.libsdl.app.EtervaleSDLActivity");
                StartActivity(intent);
                MobileDiagnosticTrace.Mark("sdl_activity_start_returned");
                await Task.Delay(1200); // primeira prova; substituir por sinal de surface-ready
                MobileDiagnosticTrace.Mark("sdl_fixed_delay_finished");
                await Task.Run(() =>
                {
                    MobileDiagnosticTrace.Mark("game_constructor_begin");
                    using var game = new PristonTale.Game1(androidSettingsPath);
                    MobileDiagnosticTrace.Mark("game_run_begin");
                    game.Run();
                });
                status.Text = "Game1 terminou.";
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                Log.Error("ETERVALE_3D", ex.ToString());
                status.Text = "Falha ao iniciar o motor 3D: " +
                    root.GetType().Name + ": " + root.Message;
            }
            finally
            {
                start.Enabled = true;
            }
        };
    }
}
