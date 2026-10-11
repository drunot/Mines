using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Mines;

/// <summary>
/// A bevelled frame around its content, joined with 45 degree mitres in the top-right and bottom-left corners
/// (corner to corner at any size).
/// </summary>
/// <remarks>
/// Sunken by default (dark top and left, light bottom and right); <see cref="IsRaised"/> gives the tiles'
/// raised look. Colours come from the theme (<c>BevelLight</c>, <c>BevelDark</c>, or their <c>Hover</c>
/// variants with <see cref="IsHighlighted"/>); the frame width is the <see cref="Decorator.Padding"/>
/// (4 by default).
/// </remarks>
public class BevelFrame : Decorator
{
    /// <summary>
    /// Defines the <see cref="IsRaised"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsRaisedProperty =
        AvaloniaProperty.Register<BevelFrame, bool>(nameof(IsRaised));

    /// <summary>
    /// Defines the <see cref="IsHighlighted"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<BevelFrame, bool>(nameof(IsHighlighted));

    /// <summary>
    /// Gets or sets whether the frame looks raised: light top and left, dark bottom and right (like a tile or
    /// button), instead of sunken.
    /// </summary>
    public bool IsRaised
    {
        get => GetValue(IsRaisedProperty);
        set => SetValue(IsRaisedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the frame uses the hover colours (<c>BevelLightHover</c>, <c>BevelDarkHover</c>),
    /// e.g. while the mouse is over a button.
    /// </summary>
    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// Sets the default frame width (a <see cref="Decorator.Padding"/> of 4) and redraws when the padding or the
    /// look changes.
    /// </summary>
    static BevelFrame()
    {
        PaddingProperty.OverrideDefaultValue<BevelFrame>(new Thickness(4));
        AffectsRender<BevelFrame>(PaddingProperty, IsRaisedProperty, IsHighlightedProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BevelFrame"/> class, which redraws when the theme changes.
    /// </summary>
    public BevelFrame()
    {
        ActualThemeVariantChanged += (sender, e) => InvalidateVisual();
    }

    /// <summary>
    /// Draws the frame: the top and left edges in one colour and the bottom and right edges in the other,
    /// clipped to slightly rounded outer corners.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_top_left is null || _bottom_right is null || Bounds.Size != _shape_size || Padding != _shape_padding)
        {
            BuildShapes(Bounds.Size, Padding);
        }

        // Slightly rounded outer corners, like the tiles' bevel.
        using (context.PushClip(new RoundedRect(new Rect(Bounds.Size), 1)))
        {
            Color light = Color(IsHighlighted ? "BevelLightHover" : "BevelLight");
            Color dark = Color(IsHighlighted ? "BevelDarkHover" : "BevelDark");
            context.DrawGeometry(CachedBrush(ref _top_left_brush, IsRaised ? light : dark), null, _top_left);
            context.DrawGeometry(CachedBrush(ref _bottom_right_brush, IsRaised ? dark : light), null, _bottom_right);
        }
    }

    /// <summary>
    /// Builds the two edge shapes for a frame of the given size and width.
    /// </summary>
    /// <param name="size">The size of the frame.</param>
    /// <param name="t">The frame width on each side.</param>
    [MemberNotNull(nameof(_top_left), nameof(_bottom_right))]
    private void BuildShapes(Size size, Thickness t)
    {
        double w = size.Width;
        double h = size.Height;

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

        _top_left = top_left;
        _bottom_right = bottom_right;
        _shape_size = size;
        _shape_padding = t;
    }

    /// <summary>
    /// Returns <paramref name="cache"/> if it already has <paramref name="color"/>, otherwise replaces it with a
    /// new brush of that colour.
    /// </summary>
    /// <param name="cache">The cached brush for one of the edges.</param>
    /// <param name="color">The colour wanted.</param>
    /// <returns>A brush of <paramref name="color"/>.</returns>
    private static IBrush CachedBrush(ref ImmutableSolidColorBrush? cache, Color color)
    {
        if (cache is null || cache.Color != color)
        {
            cache = new ImmutableSolidColorBrush(color);
        }
        return cache;
    }

    /// <summary>
    /// Looks up a colour resource from the theme.
    /// </summary>
    /// <param name="key">The resource key, e.g. <c>BevelLight</c>.</param>
    /// <returns>The colour, or magenta if the resource is missing.</returns>
    private Color Color(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is Color color ? color : Colors.Magenta;

    /// <summary>The top and left edges, for <see cref="_shape_size"/> and <see cref="_shape_padding"/>.</summary>
    private Geometry? _top_left;
    /// <summary>The bottom and right edges, for <see cref="_shape_size"/> and <see cref="_shape_padding"/>.</summary>
    private Geometry? _bottom_right;
    /// <summary>The size the edge shapes were built for.</summary>
    private Size _shape_size = new(-1, -1);
    /// <summary>The frame width the edge shapes were built for.</summary>
    private Thickness _shape_padding;
    /// <summary>The brush last used for the top and left edges.</summary>
    private ImmutableSolidColorBrush? _top_left_brush;
    /// <summary>The brush last used for the bottom and right edges.</summary>
    private ImmutableSolidColorBrush? _bottom_right_brush;
}
