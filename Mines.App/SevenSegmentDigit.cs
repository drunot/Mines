using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mines;

// One seven-segment digit (resources/Seven_Segment.Inkscape.svg). Value 0-9 lights the segments for that digit;
// null turns every segment off (an unused digit). Minus lights only the middle segment.
// Colours come from the theme: SegmentOnBrush and SegmentOffBrush.
public class SevenSegmentDigit : Control
{
    public static readonly StyledProperty<int?> ValueProperty =
        AvaloniaProperty.Register<SevenSegmentDigit, int?>(nameof(Value));

    public static readonly StyledProperty<bool> MinusProperty =
        AvaloniaProperty.Register<SevenSegmentDigit, bool>(nameof(Minus));

    // 0-9, or null for all segments off. Values outside 0-9 are shown as all off.
    public int? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // Shows a minus sign (middle segment only) when Value is null.
    public bool Minus
    {
        get => GetValue(MinusProperty);
        set => SetValue(MinusProperty, value);
    }

    static SevenSegmentDigit()
    {
        AffectsRender<SevenSegmentDigit>(ValueProperty, MinusProperty);
    }

    public SevenSegmentDigit()
    {
        ActualThemeVariantChanged += (sender, e) => InvalidateVisual();
    }

    // 24 px tall by default, with the SVG's proportions.
    protected override Size MeasureOverride(Size availableSize) => new Size(24 * SvgWidth / SvgHeight, 24);

    public override void Render(DrawingContext context)
    {
        IBrush on = Brush("SegmentOnBrush");
        IBrush off = Brush("SegmentOffBrush");
        int lit = Value is int digit && digit >= 0 && digit <= 9 ? DigitSegments[digit] : Minus ? MinusSegments : 0;

        // Fit the digit into the control, keeping its proportions, centred.
        double scale = Math.Min(Bounds.Width / SvgWidth, Bounds.Height / SvgHeight);
        var centre = new Vector((Bounds.Width - SvgWidth * scale) / 2, (Bounds.Height - SvgHeight * scale) / 2);
        using (context.PushTransform(Matrix.CreateTranslation(SvgOffset) * Matrix.CreateScale(scale, scale)
                                     * Matrix.CreateTranslation(centre)))
        {
            for (int segment = 0; segment < 7; segment++)
            {
                context.DrawGeometry((lit & (1 << segment)) != 0 ? on : off, null, Segments[segment]);
            }
        }
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush ? brush : Brushes.Magenta;

    // ---- Segments: bit 0 = a (top), then b (upper right), c (lower right), d (bottom), e (lower left),
    //      f (upper left), g (middle) ----

    private const int A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64;
    private const int MinusSegments = G;

    private static readonly int[] DigitSegments =
    {
        A | B | C | D | E | F,      // 0
        B | C,                      // 1
        A | B | G | E | D,          // 2
        A | B | G | C | D,          // 3
        F | G | B | C,              // 4
        A | F | G | C | D,          // 5
        A | F | G | E | C | D,      // 6
        A | B | C,                  // 7
        A | B | C | D | E | F | G,  // 8
        A | B | C | D | F | G,      // 9
    };

    private const double SvgWidth = 119.15862;
    private const double SvgHeight = 214.31094;
    private static readonly Vector SvgOffset = new(-36.759513, -37.746533);   // the SVG's translate(...)

    // In the order a-g, from the SVG.
    private static Geometry[] Segments => _segments ??= new[]
    {
        Geometry.Parse("m 49.459513,37.746533 -3.58066,6.202206 21.060214,21.060213 h 58.798993 l 21.06073,-21.060213 -3.58118,-6.202206 z"),
        Geometry.Parse("m 149.71592,48.70297 6.2022,3.58066 v 74.67348 L 142.54323,140.332 128.65519,126.44396 V 69.763184 Z"),
        Geometry.Parse("m 149.71592,241.10104 6.20221,-3.58065 v -74.67348 l -13.3749,-13.37489 -13.88804,13.88804 v 56.68077 z"),
        Geometry.Parse("m 143.21813,252.05748 3.58066,-6.2022 -21.06022,-21.06022 H 66.939577 l -21.06073,21.06022 3.58118,6.2022 z"),
        Geometry.Parse("m 42.961717,241.10104 -6.2022,-3.58065 v -74.67348 l 13.37489,-13.37489 13.88804,13.88804 v 56.68077 z"),
        Geometry.Parse("m 42.961719,48.70297 -6.202206,3.58066 v 74.67348 L 50.134408,140.332 64.022449,126.44396 V 69.763184 Z"),
        Geometry.Parse("m 66.947077,131.27054 -13.374894,13.37489 13.888041,13.88805 h 58.270346 l 13.37489,-13.3749 -13.88804,-13.88804 z"),
    };

    private static Geometry[]? _segments;
}
