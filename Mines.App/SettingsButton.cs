using Avalonia.Media;

namespace Mines;

// The settings button: a tile with a cog (resources/Cog.svg).
public class SettingsButton : TileButton
{
    private protected override void DrawIcon(DrawingContext context, TilePalette palette) =>
        TileShapes.DrawIcon(context, () => TileShapes.DrawCog(context, palette));
}
