using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Mines;

/// <summary>
/// Brushes and text for drawing tiles, looked up from the theme (App.axaml) for one control.
/// </summary>
/// <remarks>
/// Create a new one when the control's theme changes. Missing brush and colour resources show up as magenta.
/// </remarks>
internal sealed class TilePalette
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TilePalette"/> class, looking up the brushes, corner radius
    /// and font for <paramref name="view"/> in its current theme variant.
    /// </summary>
    /// <param name="view">The control whose resources and theme variant are used.</param>
    public TilePalette(Control view)
    {
        _view = view;
        Hidden = Brush("HiddenBrush");
        Clear = Brush("ClearBrush");
        Bomb = Brush("BombBrush");
        Hover = Brush("HoverBrush");
        FlagPole = Brush("FlagPoleBrush");
        FlagCloth = Brush("FlagClothBrush");
        BombIcon = Brush("BombIconBrush");
        QuestionIcon = Brush("QuestionIconBrush");
        CogIcon = Brush("CogIconBrush");
        Face = Brush("FaceBrush");
        FaceLine = Brush("FaceLineBrush");
        Bevel = BevelBrush("BevelLight", "BevelDark");
        BevelHover = BevelBrush("BevelLightHover", "BevelDarkHover");
        CornerRadius = view.TryFindResource("ControlCornerRadius", view.ActualThemeVariant, out object? radius)
                       && radius is CornerRadius r ? r.TopLeft : 4;
        _font = TextElement.GetFontFamily(view);
    }

    /// <summary>
    /// Gets the background of a hidden (raised) tile (<c>HiddenBrush</c>).
    /// </summary>
    public IBrush Hidden { get; }

    /// <summary>
    /// Gets the background of a revealed tile (<c>ClearBrush</c>).
    /// </summary>
    public IBrush Clear { get; }

    /// <summary>
    /// Gets the background of a revealed tile with a bomb (<c>BombBrush</c>).
    /// </summary>
    public IBrush Bomb { get; }

    /// <summary>
    /// Gets the background of a tile under the mouse (<c>HoverBrush</c>).
    /// </summary>
    public IBrush Hover { get; }

    /// <summary>
    /// Gets the brush for the flag's pole and base (<c>FlagPoleBrush</c>).
    /// </summary>
    public IBrush FlagPole { get; }

    /// <summary>
    /// Gets the brush for the flag's cloth (<c>FlagClothBrush</c>).
    /// </summary>
    public IBrush FlagCloth { get; }

    /// <summary>
    /// Gets the brush for the bomb icon (<c>BombIconBrush</c>).
    /// </summary>
    public IBrush BombIcon { get; }

    /// <summary>
    /// Gets the brush for the question mark icon (<c>QuestionIconBrush</c>).
    /// </summary>
    public IBrush QuestionIcon { get; }

    /// <summary>
    /// Gets the brush for the settings cog icon (<c>CogIconBrush</c>).
    /// </summary>
    public IBrush CogIcon { get; }

    /// <summary>
    /// Gets the fill of the reset button's face (<c>FaceBrush</c>).
    /// </summary>
    public IBrush Face { get; }

    /// <summary>
    /// Gets the brush for the face's outline and features (<c>FaceLineBrush</c>).
    /// </summary>
    public IBrush FaceLine { get; }

    /// <summary>
    /// Gets a pen for the face's outline and features (<see cref="FaceLine"/>, round caps and joins). Pens are
    /// cached per width.
    /// </summary>
    /// <param name="width">The line width, in the face SVG's coordinates.</param>
    /// <returns>The pen.</returns>
    public Pen FaceLinePen(double width)
    {
        if (!_facePens.TryGetValue(width, out Pen? pen))
        {
            pen = new Pen(FaceLine, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _facePens[width] = pen;
        }
        return pen;
    }

    /// <summary>
    /// Gets the bevel brush: <c>BevelLight</c> on the top-left half, <c>BevelDark</c> on the bottom-right half.
    /// </summary>
    public IBrush Bevel { get; }

    /// <summary>
    /// Gets the bevel brush for a tile under the mouse, from <c>BevelLightHover</c> and <c>BevelDarkHover</c>.
    /// </summary>
    public IBrush BevelHover { get; }

    /// <summary>
    /// Gets the tiles' corner radius, from the theme's <c>ControlCornerRadius</c> (4 if missing).
    /// </summary>
    public double CornerRadius { get; }

    /// <summary>
    /// Gets the text for a cell's neighbour count, in bold with that number's <c>NumberBrush</c> (e.g.
    /// <c>NumberBrush3</c>). Layouts are cached per number.
    /// </summary>
    /// <remarks>
    /// Numbers are laid out with <see cref="TextLayout"/>, like a <see cref="TextBlock"/> does, so they land where
    /// the old <see cref="TextBlock"/> did.
    /// </remarks>
    /// <param name="number">The number to show.</param>
    /// <returns>The laid-out text.</returns>
    public TextLayout Number(int number)
    {
        if (!_numbers.TryGetValue(number, out TextLayout? text))
        {
            text = new TextLayout(number.ToString(CultureInfo.InvariantCulture),
                new Typeface(_font, FontStyle.Normal, FontWeight.Bold), TileShapes.NumberFontSize,
                Brush($"NumberBrush{number}"));
            _numbers[number] = text;
        }
        return text;
    }

    /// <summary>
    /// Looks up a brush resource from the theme.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The brush, or magenta if the resource is missing.</returns>
    private IBrush Brush(string key) =>
        _view.TryFindResource(key, _view.ActualThemeVariant, out object? value) && value is IBrush brush
            ? brush
            : Brushes.Magenta;   // a missing resource should be obvious

    /// <summary>
    /// Looks up a colour resource from the theme.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>The colour, or magenta if the resource is missing.</returns>
    private Color Color(string key) =>
        _view.TryFindResource(key, _view.ActualThemeVariant, out object? value) && value is Color color
            ? color
            : Colors.Magenta;

    /// <summary>
    /// Creates a bevel brush: light top-left half, dark bottom-right half, split along the diagonal (as the old
    /// bevel border).
    /// </summary>
    /// <param name="light">The resource key of the light colour.</param>
    /// <param name="dark">The resource key of the dark colour.</param>
    /// <returns>The brush.</returns>
    private IBrush BevelBrush(string light, string dark) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color(light), 0.5),
            new GradientStop(Color(dark), 0.5),
        },
    };

    /// <summary>
    /// The control whose resources and theme variant are used.
    /// </summary>
    private readonly Control _view;

    /// <summary>
    /// The control's font family, used for the numbers.
    /// </summary>
    private readonly FontFamily _font;

    /// <summary>
    /// Cache of the laid-out numbers, by number.
    /// </summary>
    private readonly Dictionary<int, TextLayout> _numbers = new();

    /// <summary>
    /// Cache for <see cref="FaceLinePen"/>, by width.
    /// </summary>
    private readonly Dictionary<double, Pen> _facePens = new();
}

