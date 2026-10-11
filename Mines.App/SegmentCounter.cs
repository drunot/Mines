using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mines;

/// <summary>
/// A number on a row of seven-segment digits (<see cref="SevenSegmentDigit"/>), like the counters in classic
/// Minesweeper.
/// </summary>
/// <remarks>
/// Leading digits that aren't needed are switched off completely. Negative numbers get a minus in front.
/// Numbers that don't fit are capped (9999 with 4 digits, or -999 since the minus takes a digit; a single
/// digit shows just a minus for any negative number).
/// </remarks>
public class SegmentCounter : Border
{
    /// <summary>
    /// Defines the <see cref="Value"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<SegmentCounter, int>(nameof(Value));

    /// <summary>
    /// Defines the <see cref="Digits"/> property.
    /// </summary>
    public static readonly StyledProperty<int> DigitsProperty =
        AvaloniaProperty.Register<SegmentCounter, int>(nameof(Digits), 4);

    /// <summary>
    /// Gets or sets the number shown. Numbers that don't fit in <see cref="Digits"/> are capped.
    /// </summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of digits, including the one a minus sign takes. Defaults to 4; at least one
    /// digit is always shown.
    /// </summary>
    public int Digits
    {
        get => GetValue(DigitsProperty);
        set => SetValue(DigitsProperty, value);
    }

    /// <summary>
    /// Sets the counter's default look: a black background, padding 3 and corner radius 2.
    /// </summary>
    static SegmentCounter()
    {
        BackgroundProperty.OverrideDefaultValue<SegmentCounter>(Brushes.Black);
        PaddingProperty.OverrideDefaultValue<SegmentCounter>(new Thickness(3));
        CornerRadiusProperty.OverrideDefaultValue<SegmentCounter>(new CornerRadius(2));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SegmentCounter"/> class with <see cref="Digits"/> digits.
    /// </summary>
    public SegmentCounter()
    {
        Child = _panel;
        CreateDigits();
    }

    /// <summary>
    /// Recreates the digits when <see cref="Digits"/> changes and updates them when <see cref="Value"/> changes.
    /// </summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DigitsProperty)
        {
            CreateDigits();
        }
        else if (change.Property == ValueProperty)
        {
            ShowValue();
        }
    }

    /// <summary>
    /// Replaces the digit controls with <see cref="Digits"/> new ones (at least one) and shows the value on them.
    /// </summary>
    private void CreateDigits()
    {
        _panel.Children.Clear();
        for (int i = 0; i < Math.Max(1, Digits); i++)
        {
            _panel.Children.Add(new SevenSegmentDigit());
        }
        ShowValue();
    }

    /// <summary>
    /// Shows <see cref="Value"/> on the existing digits, right-aligned, capped to what fits, with a minus in
    /// front of negative numbers and unused leading digits switched off.
    /// </summary>
    private void ShowValue()
    {
        int digits = _panel.Children.Count;
        bool negative = Value < 0;
        int room = negative ? digits - 1 : digits;   // the minus sign takes one digit
        // An int has at most 10 digits, so wider counters need no cap (and 10^19 wouldn't fit in a long).
        long largest = room >= 10 ? long.MaxValue : (long)Math.Pow(10, room) - 1;
        long magnitude = Math.Min(Math.Abs((long)Value), largest);
        string number = room > 0 ? magnitude.ToString(CultureInfo.InvariantCulture) : "";   // no room: only the minus
        string text = (negative ? "-" : "") + number;
        text = text.Length > digits ? text[^digits..] : text.PadLeft(digits);

        for (int i = 0; i < digits; i++)
        {
            var digit = (SevenSegmentDigit)_panel.Children[i];
            char c = text[i];
            digit.Value = char.IsDigit(c) ? c - '0' : null;   // ' ' and '-' have no digit: all off ...
            digit.Minus = c == '-';                           // ... except the middle segment for '-'
        }
    }

    /// <summary>
    /// The row of <see cref="SevenSegmentDigit"/> controls; the counter's child.
    /// </summary>
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
}
