using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PristonTale.Client.Net;
using PristonTale.Engine3D.Diagnostics;
using PristonTale.Engine3D.Rendering;
using PristonTale.Gameplay.Screens.Opening;
using PristonTale.Gameplay.UI.Rendering;
using PristonTale.Gameplay.UI;
using PristonTale.Gameplay.Audio;
using PristonTale.Gameplay.Localization;

namespace PristonTale.Gameplay.Screens.Framework;

public sealed class GameClient : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentManager _content;
    private readonly string _assetRoot;
    private readonly string _serverAddress;

    private SpriteBatch _spriteBatch;
    private IGameBackend _backend;
    private ScreenContext _context;
    private GameScreen _screen;
    private GameScreen _pendingScreen;
    private Task _shutdown;
    private SpriteFont _font;
    private HudAtlas _uiAtlas;
    private HudCursor _cursor;

    public bool HasGameCursor => _cursor?.HasDefaultArt == true;
    private PristonSkyLibrary _sky;
    private PristonTale.Gameplay.Effects.EffectLibrary _effects;
    private PristonAudioLibrary _audio;
    private readonly PresentationSettings _presentation;
    private bool _h700LazyBoot;
    private bool _h700HudRequested;
    private string _h700LazyStatus = "Arranque minimo pronto";
    private DisplayConfirmation _displayConfirmation;

    public GameClient(GraphicsDevice graphicsDevice, ContentManager content, string assetRoot, string serverAddress, PresentationSettings presentation = null)
    {
        _graphicsDevice = graphicsDevice;
        _content = content;
        _assetRoot = assetRoot;
        _serverAddress = serverAddress;
        _presentation = presentation ?? new PresentationSettings();
        ClientText.Use(_presentation.Requested.Language);
    }

    public string StatusText
    {
        get
        {
            if (_shutdown is not null)
            {
                return ClientText.Get("Status.SavingAndDisconnecting");
            }

            return _screen?.StatusText ?? ClientText.Get("Status.Loading");
        }
    }

    public void LoadContent()
    {
        // ETERVALE_H700_LITE_V1_MARKER
        if (System.Environment.GetEnvironmentVariable("ETERVALE_H700_LITE") == "1")
        {
            System.Environment.SetEnvironmentVariable("ETERVALE_H700_SKIP_AUDIO", "1");
            System.Environment.SetEnvironmentVariable("ETERVALE_H700_SKIP_SKY", "1");
            System.Environment.SetEnvironmentVariable("ETERVALE_H700_SKIP_VFX", "1");
            System.Environment.SetEnvironmentVariable("ETERVALE_H700_LOW_FX", "1");
            System.Environment.SetEnvironmentVariable("ETERVALE_H700_LAZY_SURFACES", "1");
            ConsoleClientLog.Instance.Warn("[H700 LITE] Perfil v1 ativo: cena minima, sem audio, ceu, VFX ou efeitos opcionais.");
        }
        void H700Mark(string stage)
        {
            if (System.Environment.GetEnvironmentVariable(
                    "ETERVALE_H700_TRACE_MEM") != "1")
                return;

            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var rss = process.WorkingSet64 / 1048576;
            var heap = System.GC.GetTotalMemory(false) / 1048576;

            ConsoleClientLog.Instance.Info(
                $"[H700 MEM] {stage}: RSS={rss} MiB; heap={heap} MiB");
        }

        H700Mark("inicio LoadContent");
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        var font = _content.Load<SpriteFont>("Fonts/UI");
        _font = font;
        if (Environment.GetEnvironmentVariable("ETERVALE_H700_LAZY_BOOT") == "1")
        {
            _h700LazyBoot = true;
            H700LazyLog("Arranque minimo: sem atlas, ceu, VFX, audio nem cena 3D");
            return;
        }
        _uiAtlas = HudAtlas.LoadBootstrap(_graphicsDevice, _assetRoot, ConsoleClientLog.Instance);
        _cursor = HudCursor.Load(_graphicsDevice, _assetRoot, ConsoleClientLog.Instance);
        H700Mark("depois HUD e cursor");
        if (System.Environment.GetEnvironmentVariable("ETERVALE_H700_SKIP_SKY") == "1")
        {
            ConsoleClientLog.Instance.Warn("[H700] Sky bootstrap ignorado para poupar RAM.");
            _sky = null;
        }
        else
        {
            _sky = PristonSkyLibrary.LoadBootstrap(_graphicsDevice, _assetRoot, ConsoleClientLog.Instance);
        }
        H700Mark("antes de VFX");
        if (System.Environment.GetEnvironmentVariable("ETERVALE_H700_SKIP_VFX") == "1")
        {
            ConsoleClientLog.Instance.Warn("[H700] VFX bootstrap ignorado.");
            _effects = null;
        }
        else
        {
            _effects = PristonTale.Gameplay.Effects.EffectLibrary.LoadBootstrap(
                _graphicsDevice, _content, _assetRoot, ConsoleClientLog.Instance);
        }
        H700Mark("depois de VFX");
        _audio = PristonAudioLibrary.LoadBootstrap(_assetRoot, ConsoleClientLog.Instance);
        _audio.ApplySettings(_presentation.Requested);
        H700Mark("depois audio");
        _backend = new GameServerConnection(_serverAddress);
        _context = new ScreenContext(
            _graphicsDevice,
            _content,
            _spriteBatch,
            font,
            _assetRoot,
            _backend,
            ConsoleClientLog.Instance,
            RequestScreen,
            _presentation,
            _uiAtlas,
            _sky,
            _effects,
            _audio,
            RenewBackend);
        _displayConfirmation = new DisplayConfirmation(_context);
        // First screen: the opening / authentication scene (the login form overlays it next).
        H700Mark("antes OpeningScreen");
        SetScreen(new OpeningScreen(_context));
        H700Mark("depois OpeningScreen");
    }

    public bool HandleEscape() => _screen?.HandleEscape() == true;

    // The diagnostics overlay (F9, as the original's debug display): the frame timing and the screen's status, drawn at
    // the top left over everything.
    public bool DiagnosticsShown { get; set; }
    public string Diagnostics { get; set; } = string.Empty;

    // A character from the window's text input (the chat line reads it).
    public void TypeCharacter(char value) => _context?.TypedText.Push(value);

    // The server asked the client to save and quit.
    public bool ExitRequested => _context?.ExitRequested == true;

    public void Update(GameTime gameTime, bool isWindowActive = true)
    {
        if (_h700LazyBoot)
        {
            H700LazyUpdate();
            return;
        }
        ClientText.Use(_presentation.Requested.Language);
        _audio?.ApplySettings(_presentation.Requested);
        if (_context is not null)
        {
            _context.IsWindowActive = isWindowActive;
        }

        _displayConfirmation?.Update();
        _screen?.Update(gameTime);
        if (_pendingScreen != null)
        {
            var next = _pendingScreen;
            _pendingScreen = null;
            SetScreen(next);
        }
    }

    public void Draw()
    {
        if (_h700LazyBoot)
        {
            _graphicsDevice.Clear(new Color(16, 36, 70));
            _spriteBatch.Begin();
            _spriteBatch.DrawString(_font,
                "ETERVALE H700\n" + _h700LazyStatus
                + "\nHUD: " + (_uiAtlas is null ? "nao solicitado" : _uiAtlas.IsReady ? "pronto" : _uiAtlas.Failed ? "falhou" : "a carregar")
                + "\nSSH: touch vulkan-test/h700-load-hud",
                new Vector2(18f, 18f), Color.White);
            _spriteBatch.End();
            return;
        }
        if (_shutdown is null)
        {
            _screen?.Draw();
            _displayConfirmation?.Draw();
            DrawDiagnostics();
            // Draw after every dialog and overlay, using the source cursor pixels rather than an SDL cursor.
            _cursor?.Draw(_spriteBatch, Mouse.GetState().Position, _screen?.Cursor ?? HudCursorKind.Default);
            return;
        }

        _graphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();
        _spriteBatch.DrawString(_font, StatusText, new Vector2(24f, 24f), Color.White);
        _spriteBatch.End();
    }

    // Diagnostic staging: the first frame uses only MonoGame's Font and SpriteBatch.
    // The existing HudAtlas async constructor then prepares images on a worker and its
    // Update method uploads at most 128 rows at a time on the render thread.
    // It still decodes the atlas all at once on its worker: full fine-grained
    // texture-by-texture decoding will require a subsequent refactor.
    private void H700LazyUpdate()
    {
        if (!_h700HudRequested)
        {
            var request = Path.Combine(AppContext.BaseDirectory, "vulkan-test", "h700-load-hud");
            if (!File.Exists(request))
            {
                return;
            }

            File.Delete(request);
            _h700HudRequested = true;
            _h700LazyStatus = "Preparacao do atlas HUD iniciada";
            H700LazyLog("A criar HUD assíncrono sob pedido");
            _uiAtlas = new HudAtlas(_graphicsDevice, _assetRoot, ConsoleClientLog.Instance);
            return;
        }

        if (_uiAtlas is null || _uiAtlas.IsReady || _uiAtlas.Failed)
        {
            return;
        }

        _uiAtlas.Update();
        if (_uiAtlas.IsReady)
        {
            _h700LazyStatus = "HUD pronto; cena 3D nao carregada";
            H700LazyLog($"HUD pronto; GPU texture bytes={_uiAtlas.TextureBytes}");
        }
        else if (_uiAtlas.Failed)
        {
            _h700LazyStatus = "ERRO no HUD - ver log";
            H700LazyLog("Falha na preparacao do HUD");
        }
    }

    private static void H700LazyLog(string stage)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var rss = process.WorkingSet64 / (1024 * 1024);
        var heap = GC.GetTotalMemory(false) / (1024 * 1024);
        ConsoleClientLog.Instance.Info($"[H700 LAZY] {stage}; RSS={rss} MiB; heap={heap} MiB");
    }

    private void DrawDiagnostics()
    {
        if (!DiagnosticsShown || Diagnostics.Length == 0)
        {
            return;
        }

        _spriteBatch.Begin();
        _spriteBatch.DrawString(_font, Diagnostics, new Vector2(9f, 9f), Color.Black);
        _spriteBatch.DrawString(_font, Diagnostics, new Vector2(8f, 8f), Color.White);
        _spriteBatch.End();
    }

    private void RequestScreen(GameScreen next) => _pendingScreen = next;

    private void SetScreen(GameScreen next)
    {
        _screen?.Dispose();
        _screen = next;
        _screen.Enter();
    }

    private void RenewBackend()
    {
        var previous = _backend;
        _backend = new GameServerConnection(_serverAddress);
        _context.ReplaceBackend(_backend);
        _ = Task.Run(() => DisposeBackendAsync(previous));
    }

    private static async Task DisposeBackendAsync(IGameBackend backend)
    {
        try
        {
            await backend.DisposeAsync();
        }
        catch (Exception ex)
        {
            ConsoleClientLog.Instance.Error($"[Client] backend shutdown failed: {ex}");
        }
    }

    public Task BeginShutdown()
    {
        if (_shutdown is not null)
        {
            return _shutdown;
        }

        // GPU resources are released on the game thread. Network/database cleanup runs asynchronously, after the screen's
        // closing work (leaving the world, so the server stores the character) has used the connection.
        var screen = _screen;
        screen?.Dispose();
        var closing = screen?.Closing ?? Task.CompletedTask;
        _screen = null;
        _pendingScreen = null;
        var backend = _backend;
        _shutdown = backend is null ? closing : Task.Run(async () =>
        {
            try
            {
                await closing;
            }
            finally
            {
                await DisposeBackendAsync(backend);
            }
        });
        return _shutdown;
    }

    public void Dispose()
    {
        _ = BeginShutdown();
        _uiAtlas?.Dispose();
        _cursor?.Dispose();
        _sky?.Dispose();
        _effects?.Dispose();
        _audio?.Dispose();
        _spriteBatch?.Dispose();
    }
}
