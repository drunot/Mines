using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mines;

/// <summary>
/// The colon between two groups of seven-segment digits (resources/Seven_Segment_Colon.Inkscape.svg).
/// </summary>
/// <remarks>
/// As tall as a <see cref="SevenSegmentDigit"/>, so the two line up. Colours come from the theme:
/// <c>SegmentOnBrush</c> when <see cref="IsOn"/>, otherwise <c>SegmentOffBrush</c>.
/// </remarks>
public class SevenSegmentColon : Control
{
    /// <summary>
    /// Defines the <see cref="IsOn"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<SevenSegmentColon, bool>(nameof(IsOn), true);

    /// <summary>
    /// Gets or sets whether the colon is lit. Defaults to <c>true</c>.
    /// </summary>
    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>
    /// Makes the colon redraw when <see cref="IsOn"/> changes.
    /// </summary>
    static SevenSegmentColon()
    {
        AffectsRender<SevenSegmentColon>(IsOnProperty);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SevenSegmentColon"/> class, which redraws when the theme
    /// changes.
    /// </summary>
    public SevenSegmentColon()
    {
        ActualThemeVariantChanged += (sender, e) => InvalidateVisual();
    }

    /// <summary>
    /// Asks for 24 px tall by default, like <see cref="SevenSegmentDigit"/>, with the SVG's proportions.
    /// </summary>
    /// <param name="availableSize">The available size (ignored).</param>
    /// <returns>The desired size.</returns>
    protected override Size MeasureOverride(Size availableSize) => new Size(24 * SvgWidth / SvgHeight, 24);

    /// <summary>
    /// Draws the two dots, lit or unlit, scaled to fit the control and centred.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context)
    {
        IBrush brush = Brush(IsOn ? "SegmentOnBrush" : "SegmentOffBrush");

        // Fit the colon into the control, keeping its proportions, centred.
        double scale = Math.Min(Bounds.Width / SvgWidth, Bounds.Height / SvgHeight);
        var centre = new Vector((Bounds.Width - SvgWidth * scale) / 2, (Bounds.Height - SvgHeight * scale) / 2);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(centre)))
        {
            context.DrawEllipse(brush, null, UpperDot, DotRadius, DotRadius);
            context.DrawEllipse(brush, null, LowerDot, DotRadius, DotRadius);
        }
    }

    /// <summary>
    /// Looks up a brush resource from the theme.
    /// </summary>
    /// <param name="key">The resource key, e.g. <c>SegmentOnBrush</c>.</param>
    /// <returns>The brush, or magenta if the resource is missing.</returns>
    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush ? brush : Brushes.Magenta;

    /// <summary>
    /// The width of the colon in the SVG's coordinates.
    /// </summary>
    private const double SvgWidth = 30.930246;

    /// <summary>
    /// The height of the colon in the SVG's coordinates; the same as the digit's.
    /// </summary>
    private const double SvgHeight = 214.31094;

    /// <summary>
    /// The radius of the dots.
    /// </summary>
    private const double DotRadius = 15.465123;

    /// <summary>
    /// The centre of the upper dot, with the SVG's two translate(...)s already applied.
    /// </summary>
    private static readonly Point UpperDot = new(96.338821 - 80.873698, 102.79102 + 0.66145381 - 37.746533);

    /// <summary>
    /// The centre of the lower dot, with the SVG's two translate(...)s already applied.
    /// </summary>
    private static readonly Point LowerDot = new(96.338821 - 80.873698, 185.69008 + 0.66145381 - 37.746533);
}
