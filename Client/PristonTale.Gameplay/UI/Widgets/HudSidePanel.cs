using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI.Rendering;

namespace PristonTale.Gameplay.UI.Widgets;

// The place the party window and its quest page share: 256x256 at (539, 155) of the 800x600 frame (right and bottom
// anchored), sliding in from the right edge, with a row of 20 px buttons at the bottom.
public static class HudSidePanel
{
    public const int Width = 256;
    public const int Height = 256;
    private const int DesignX = 539;
    private const int DesignY = 155;

    // ETERVALE_MOBILE_SAFE_GRID_SHORTCUTS_V34: Party e Quest centrados também em Android.
    public static Point Origin(HudLayout layout, int slideMove = HudSlide.Full)
    {
        int move=Math.Clamp(slideMove,0,HudSlide.Full);
        if(OperatingSystem.IsAndroid())
        {
            int centerX=(layout.Width-Width)/2;
            int startX=layout.Width+8;
            int x=centerX+(startX-centerX)*(HudSlide.Full-move)/HudSlide.Full;
            return new Point(x,(layout.Height-Height)/2);
        }
        return new Point(layout.Width-(HudLayout.DesignWidth-DesignX)*move/HudSlide.Full,
            DesignY+layout.BottomOffset);
    }

    // The panel with its title plate above it.
    public static Rectangle Bounds(Point origin) => new(origin.X, origin.Y - 8, Width, Height + 8);

    public static Rectangle Button(Point origin, int index) => new(origin.X + 100 + index * 23, origin.Y + 220, 20, 20);
}
