using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mines;

// A bevelled frame around its content, joined with 45 degree mitres in the top-right and bottom-left corners
// (corner to corner at any size). Sunken by default (dark top and left, light bottom and right); IsRaised
// gives the tiles' raised look. Colours come from the theme (BevelLight, BevelDark, or their Hover variants
// with IsHighlighted); the frame width is the Padding.
public class BevelFrame : Decorator
{
    public static readonly StyledProperty<bool> IsRaisedProperty =
        AvaloniaProperty.Register<BevelFrame, bool>(nameof(IsRaised));

    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<BevelFrame, bool>(nameof(IsHighlighted));

    // Light top and left, dark bottom and right (like a tile or button), instead of sunken.
    public bool IsRaised
    {
        get => GetValue(IsRaisedProperty);
        set => SetValue(IsRaisedProperty, value);
    }

    // Uses the hover colours (BevelLightHover, BevelDarkHover), e.g. while the mouse is over a button.
    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    static BevelFrame()
    {
        PaddingProperty.OverrideDefaultValue<BevelFrame>(new Thickness(4));
        AffectsRender<BevelFrame>(PaddingProperty, IsRaisedProperty, IsHighlightedProperty);
    }

    public BevelFrame()
    {
        ActualThemeVariantChanged += (sender, e) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double w = Bounds.Width;
        double h = Bounds.Height;
        Thickness t = Padding;

        // Top and left edges.
        var top_left = new StreamGeometry();
        using (StreamGeometryContext g = top_left.Open())
        {
            g.BeginFigure(new Point(0, 0), true);
            g.LineTo(new Point(w, 0));
            g.LineTo(new Point(w - t.Right, t.Top));
            g.LineTo(new Point(t.Left, t.Top));
            g.LineTo(new Point(t.Left, h - t.Bottom));
            g.LineTo(new Point(0, h));
            g.EndFigure(true);
        }

        // Bottom and right edges.
        var bottom_right = new StreamGeometry();
        using (StreamGeometryContext g = bottom_right.Open())
        {
            g.BeginFigure(new Point(w, 0), true);
            g.LineTo(new Point(w, h));
            g.LineTo(new Point(0, h));
            g.LineTo(new Point(t.Left, h - t.Bottom));
            g.LineTo(new Point(w - t.Right, h - t.Bottom));
            g.LineTo(new Point(w - t.Right, t.Top));
            g.EndFigure(true);
        }

        // Slightly rounded outer corners, like the tiles' bevel.
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 1)))
        {
            Color light = Color(IsHighlighted ? "BevelLightHover" : "BevelLight");
            Color dark = Color(IsHighlighted ? "BevelDarkHover" : "BevelDark");
            context.DrawGeometry(new SolidColorBrush(IsRaised ? light : dark), null, top_left);
            context.DrawGeometry(new SolidColorBrush(IsRaised ? dark : light), null, bottom_right);
        }
    }

    private Color Color(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is Color color ? color : Colors.Magenta;
}
