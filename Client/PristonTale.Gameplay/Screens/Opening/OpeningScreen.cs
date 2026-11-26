using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using PristonTale.Gameplay.Audio;
using PristonTale.Gameplay.Screens.CharacterSelect;
using PristonTale.Gameplay.Screens.Framework;
using PristonTale.Gameplay.Localization;

namespace PristonTale.Gameplay.Screens.Opening;

public sealed class OpeningScreen : GameScreen
{
    private OpeningCinematic _cinematic;
    private OpeningLoginController _loginController;

    private LoginAudio _audio;
    private KeyboardState _lastKeyboard;
    private string _status;

    private bool _loginRevealed;
    private bool _mobileLoginDrawn;
    private ButtonState _lastIntroButton;
    private bool _waitForIntroRelease;
    private string _introHint = string.Empty;
    private Vector2 _introHintSize;

    public OpeningScreen(ScreenContext context) : base(context)
    {
    }

    public override string StatusText => _status;

    public override void Enter()
    {
        // Handheld-only: establish a usable login before allocating any 3D renderer.
        if (System.Environment.GetEnvironmentVariable("ETERVALE_H700_DIRECT_LOGIN") == "1")
        {
            _audio = LoginAudio.Opening(Context.Audio);
            _loginController = new OpeningLoginController(Context, _audio);
            _loginRevealed = true;
            _status = "H700 login 2D";
            Context.Log.Info("[H700 LOGIN] Login 2D ativo; renderer e cinematica adiados.");
            return;
        }
        _cinematic = new OpeningCinematic(
            Context.GraphicsDevice,
            Context.Content,
            Context.Log,
            AssetPath,
            Context.AssetRoot,
            Context.Sky,
            Context.Presentation,
            Context.Effects);
        _cinematic.Load();
        _audio = LoginAudio.Opening(Context.Audio);
        _loginController = new OpeningLoginController(Context, _audio);
        _status = ClientText.Format("Status.Opening", _cinematic.StatusSuffix);
        if (OperatingSystem.IsAndroid())
        {
            _introHint = ClientText.Get("Login.SkipIntro");
            _introHintSize = Context.Font.MeasureString(_introHint);
        }
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _audio.Update(dt);

        _cinematic?.Update(gameTime);

        var keyboard = Keyboard.GetState();
        var android = OperatingSystem.IsAndroid();
        // GameClient updates the menu's virtual mouse from SDL touch events before this screen.
        var button = android ? Mouse.GetState().LeftButton : ButtonState.Released;
        if (Context.Presentation.AwaitingConfirmation)
        {
            _lastKeyboard = keyboard;
            _lastIntroButton = ButtonState.Released;
            return;
        }

        if (!_loginRevealed)
        {
            if (AnyKeyPressed(keyboard, _lastKeyboard) || _cinematic?.IsOver == true
                || IntroTouchReleased(android, Context.IsWindowActive, _lastIntroButton, button))
            {
                _loginRevealed = true;
                _waitForIntroRelease = android && button == ButtonState.Pressed;
            }
        }
        else if (_waitForIntroRelease)
        {
            // A key or the cinematic ending may reveal login while a finger is still down.
            _waitForIntroRelease = button == ButtonState.Pressed;
        }
        else if (_loginController.Update(keyboard, _lastKeyboard, dt) == OpeningLoginController.LoginResult.Succeeded)
        {
            Context.RequestScreen(new CharacterSelectScreen(Context));
        }

        _lastIntroButton = Context.IsWindowActive ? button : ButtonState.Released;
        _lastKeyboard = keyboard;
    }

    internal static bool IntroTouchReleased(bool android, bool active, ButtonState previous, ButtonState current)
        => android && active && previous == ButtonState.Pressed && current == ButtonState.Released;

    private static bool AnyKeyPressed(in KeyboardState current, in KeyboardState previous)
    {
        foreach (var key in current.GetPressedKeys())
            if (!previous.IsKeyDown(key))
                return true;
        return false;
    }

    public override void Draw()
    {
        if (_cinematic is null)
            Context.GraphicsDevice.Clear(new Color(16, 36, 70));
        else
            _cinematic.Draw();

        var viewport = Context.GraphicsDevice.Viewport;
        if (!_loginRevealed)
        {
            Context.SpriteBatch.Begin();
            if (OperatingSystem.IsAndroid() && viewport.Width > 0 && viewport.Height > 0)
            {
                var scale = Math.Min(viewport.Width * 0.8f / Math.Max(1f, _introHintSize.X),
                    viewport.Height * 0.045f / Math.Max(1f, _introHintSize.Y));
                var position = new Vector2((viewport.Width - _introHintSize.X * scale) / 2f,
                    viewport.Height * 0.9f - _introHintSize.Y * scale / 2f);
                Context.SpriteBatch.DrawString(Context.Font, _introHint, position + Vector2.One * 2f, Color.Black,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                Context.SpriteBatch.DrawString(Context.Font, _introHint, position, Color.White,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            Context.SpriteBatch.End();
        }
        else
        {
            _loginController.Draw(viewport);
            if (OperatingSystem.IsAndroid() && !_mobileLoginDrawn)
            {
                _mobileLoginDrawn = true;
                PristonTale.Engine3D.Diagnostics.MobileDiagnosticTrace.Mark("login_first_draw");
            }
        }
    }

    public override void Dispose()
    {
        _loginController?.Dispose();
        _audio?.Dispose();
        _cinematic?.Dispose();
    }
}
