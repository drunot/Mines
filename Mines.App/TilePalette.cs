using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Mines;

// Brushes and text for drawing tiles, looked up from the theme (App.axaml) for one control.
// Create a new one when the control's theme changes.
internal sealed class TilePalette
{
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

    public IBrush Hidden { get; }
    public IBrush Clear { get; }
    public IBrush Bomb { get; }
    public IBrush Hover { get; }
    public IBrush FlagPole { get; }
    public IBrush FlagCloth { get; }
    public IBrush BombIcon { get; }
    public IBrush QuestionIcon { get; }
    public IBrush CogIcon { get; }
    public IBrush Face { get; }
    public IBrush FaceLine { get; }
    public IBrush Bevel { get; }
    public IBrush BevelHover { get; }
    public double CornerRadius { get; }

    // Numbers are laid out with TextLayout, like a TextBlock does, so they land where the old TextBlock did.
    public TextLayout Number(int number)
    {
        if (!_numbers.TryGetValue(number, out TextLayout? text))
        {
            text = new TextLayout(number.ToString(CultureInfo.InvariantCulture),
                new Typeface(_font, FontStyle.Normal, FontWeight.Bold), TileShapes.NumberFontSize, Brush($"NumberBrush{number}"));
            _numbers[number] = text;
        }
        return text;
    }

    private IBrush Brush(string key) =>
        _view.TryFindResource(key, _view.ActualThemeVariant, out object? value) && value is IBrush brush
            ? brush
            : Brushes.Magenta;   // a missing resource should be obvious

    private Color Color(string key) =>
        _view.TryFindResource(key, _view.ActualThemeVariant, out object? value) && value is Color color
            ? color
            : Colors.Magenta;

    // Light top-left half, dark bottom-right half, split along the diagonal (as the old bevel border).
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

    private readonly Control _view;
    private readonly FontFamily _font;
    private readonly Dictionary<int, TextLayout> _numbers = new();
}

