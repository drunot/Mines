using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mines;

// A number on a row of seven-segment digits, like the counters in classic Minesweeper.
// Leading digits that aren't needed are switched off completely. Negative numbers get a minus in front.
// Numbers that don't fit are capped (9999 with 4 digits, or -999 since the minus takes a digit).
public class SegmentCounter : Border
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<SegmentCounter, int>(nameof(Value));

    public static readonly StyledProperty<int> DigitsProperty =
        AvaloniaProperty.Register<SegmentCounter, int>(nameof(Digits), 4);

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Digits
    {
        get => GetValue(DigitsProperty);
        set => SetValue(DigitsProperty, value);
    }

    static SegmentCounter()
    {
        BackgroundProperty.OverrideDefaultValue<SegmentCounter>(Brushes.Black);
        PaddingProperty.OverrideDefaultValue<SegmentCounter>(new Thickness(3));
        CornerRadiusProperty.OverrideDefaultValue<SegmentCounter>(new CornerRadius(2));
    }

    public SegmentCounter()
    {
        Child = _panel;
        CreateDigits();
    }

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

    private void CreateDigits()
    {
        _panel.Children.Clear();
        for (int i = 0; i < Math.Max(1, Digits); i++)
        {
            _panel.Children.Add(new SevenSegmentDigit());
        }
        ShowValue();
    }

    private void ShowValue()
    {
        int digits = _panel.Children.Count;
        bool negative = Value < 0;
        int room = negative ? digits - 1 : digits;   // the minus sign takes one digit
        long largest = (long)Math.Pow(10, room) - 1;
        long magnitude = Math.Min(Math.Abs((long)Value), largest);
        string text = (negative && room > 0 ? "-" : "") + magnitude.ToString(CultureInfo.InvariantCulture);
        text = text.Length > digits ? text[^digits..] : text.PadLeft(digits);

        for (int i = 0; i < digits; i++)
        {
            var digit = (SevenSegmentDigit)_panel.Children[i];
            char c = text[i];
            digit.Value = char.IsDigit(c) ? c - '0' : null;   // ' ' and '-' have no digit: all off ...
            digit.Minus = c == '-';                           // ... except the middle segment for '-'
        }
    }

    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
}
