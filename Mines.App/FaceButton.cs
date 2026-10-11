using Avalonia;
using Avalonia.Media;

namespace Mines;

// The reset button: a tile with a face (resources/Faces.Inkscape.svg) showing the game state.
public class FaceButton : TileButton
{
    public static readonly StyledProperty<Face> FaceProperty =
        AvaloniaProperty.Register<FaceButton, Face>(nameof(Face), Face.Normal);

    public Face Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    static FaceButton()
    {
        AffectsRender<FaceButton>(FaceProperty);
    }

    private protected override void DrawIcon(DrawingContext context, TilePalette palette) =>
        TileShapes.DrawIcon(context, () => TileShapes.DrawFace(context, palette, Face), apply_svg_offset: false);
}
