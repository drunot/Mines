using Avalonia;
using Avalonia.Media;

namespace Mines;

/// <summary>
/// The reset button: a tile with a face (resources/Faces.Inkscape.svg) showing the game state.
/// </summary>
public class FaceButton : TileButton
{
    /// <summary>
    /// Defines the <see cref="Face"/> property.
    /// </summary>
    public static readonly StyledProperty<Face> FaceProperty =
        AvaloniaProperty.Register<FaceButton, Face>(nameof(Face), Face.Normal);

    /// <summary>
    /// Gets or sets the face shown on the button. Defaults to <see cref="Mines.Face.Normal"/>.
    /// </summary>
    public Face Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    /// <summary>
    /// Makes the button redraw when <see cref="Face"/> changes.
    /// </summary>
    static FaceButton()
    {
        AffectsRender<FaceButton>(FaceProperty);
    }

    /// <summary>
    /// Draws the current <see cref="Face"/> in the tile's icon area.
    /// </summary>
    /// <param name="context">The drawing context, in tile units.</param>
    /// <param name="palette">The theme brushes to draw with.</param>
    private protected override void DrawIcon(DrawingContext context, TilePalette palette) =>
        TileShapes.DrawIcon(context, () => TileShapes.DrawFace(context, palette, Face), apply_svg_offset: false);
}
