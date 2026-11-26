using Microsoft.Xna.Framework;

namespace PristonTale.Gameplay.UI;

public readonly record struct LoginLayout(float Scale, Rectangle Panel)
{
    private const int PanelWidth = 480;
    private const int ClassicHeight = 720;
    private const int CompactHeight = 340;
    private const float MobileFormFraction = 0.45f;

    public bool Compact { get; init; }
    public Rectangle Title => At(62, Compact ? 4 : 267, 356, 40);
    public Rectangle Subtitle => At(62, Compact ? 46 : 306, 356, 28);
    public Rectangle Account => At(78, Compact ? 104 : 367, 324, 42);
    public Rectangle Password => At(78, Compact ? 176 : 439, 324, 42);
    public Rectangle Login => At(78, Compact ? 238 : 501, 324, 44);
    public Rectangle Status => At(78, Compact ? 286 : 558, 324, 24);
    public Rectangle Hint => At(78, Compact ? 312 : 586, 324, 18);

    public static LoginLayout Create(int width, int height) => Create(width, height, OperatingSystem.IsAndroid());

    internal static LoginLayout Create(int width, int height, bool mobile)
    {
        if (mobile)
        {
            // The fullscreen Android window uses adjustNothing; the IME overlays the drawable.
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var availableHeight = Math.Max(1f, height * MobileFormFraction);
            var margin = Math.Min(12f, Math.Min(width, availableHeight) / 4f);
            var mobileScale = Math.Min((width - 2 * margin) / PanelWidth, (availableHeight - 2 * margin) / CompactHeight);
            var left = (int)((width / mobileScale - PanelWidth) / 2);
            var top = (int)((availableHeight / mobileScale - CompactHeight) / 2);
            return new LoginLayout(mobileScale, new Rectangle(left, top, PanelWidth, CompactHeight)) { Compact = true };
        }

        var fit = Math.Min((width - 24) / (float)PanelWidth, (height - 24) / (float)ClassicHeight);
        var scale = Math.Max(0.1f, Math.Min(1f, fit));
        var x = (int)((width / scale - PanelWidth) / 2);
        var y = (int)((height / scale - ClassicHeight) / 2);
        return new LoginLayout(scale, new Rectangle(x, y, PanelWidth, ClassicHeight));
    }

    public Point Pointer(Point drawable) => new((int)(drawable.X / Scale), (int)(drawable.Y / Scale));

    private Rectangle At(int x, int y, int width, int height) => new(Panel.X + x, Panel.Y + y, width, height);
}
