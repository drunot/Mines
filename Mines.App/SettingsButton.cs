using Avalonia.Media;

namespace Mines;

/// <summary>
/// The settings button: a tile with a cog (resources/Cog.svg).
/// </summary>
public class SettingsButton : TileButton
{
    /// <summary>
    /// Draws the cog in the tile's icon area.
    /// </summary>
    /// <param name="context">The drawing context, in tile units.</param>
    /// <param name="palette">The theme brushes to draw with.</param>
    private protected override void DrawIcon(DrawingContext context, TilePalette palette) =>
        TileShapes.DrawIcon(context, () => TileShapes.DrawCog(context, palette));
}
