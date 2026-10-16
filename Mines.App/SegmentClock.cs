using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mines;

/// <summary>
/// A time as hh:mm:ss on seven-segment digits (<see cref="SevenSegmentDigit"/>) and colons
/// (<see cref="SevenSegmentColon"/>), with the same look as <see cref="SegmentCounter"/>.
/// </summary>
/// <remarks>
/// Only the parts in use are lit: under a minute just the seconds, under an hour minutes and seconds.
/// Unused leading digits are switched off, and so is a colon with nothing lit to its left.
/// Times of 100 hours or more are capped at 99:59:59.
/// </remarks>
public class SegmentClock : Border
{
    /// <summary>
    /// Defines the <see cref="Seconds"/> property.
    /// </summary>
    public static readonly StyledProperty<int> SecondsProperty =
        AvaloniaProperty.Register<SegmentClock, int>(nameof(Seconds));

    /// <summary>
    /// Gets or sets the time shown, in whole seconds. Negative values show as 0.
    /// </summary>
    public int Seconds
    {
        get => GetValue(SecondsProperty);
        set => SetValue(SecondsProperty, value);
    }

    /// <summary>
    /// Sets the clock's default look, the same as <see cref="SegmentCounter"/>: a black background, padding 3
    /// and corner radius 2.
    /// </summary>
    static SegmentClock()
    {
        BackgroundProperty.OverrideDefaultValue<SegmentClock>(Brushes.Black);
        PaddingProperty.OverrideDefaultValue<SegmentClock>(new Thickness(3));
        CornerRadiusProperty.OverrideDefaultValue<SegmentClock>(new CornerRadius(2));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SegmentClock"/> class: hh:mm:ss, showing 0.
    /// </summary>
    public SegmentClock()
    {
        for (int i = 0; i < _digits.Length; i++)
        {
            _digits[i] = new SevenSegmentDigit();
        }
        _hoursColon = new SevenSegmentColon();
        _minutesColon = new SevenSegmentColon();

        _panel.Children.AddRange(new Control[]
        {
            _digits[0], _digits[1], _hoursColon, _digits[2], _digits[3], _minutesColon, _digits[4], _digits[5],
        });
        Child = _panel;
        ShowTime();
    }

    /// <summary>
    /// Updates the digits when <see cref="Seconds"/> changes.
    /// </summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SecondsProperty)
        {
            ShowTime();
        }
    }

    /// <summary>
    /// Shows <see cref="Seconds"/> as hh:mm:ss, lighting only the parts in use.
    /// </summary>
    private void ShowTime()
    {
        int total = Math.Clamp(Seconds, 0, MaxSeconds);
        int hours = total / 3600;
        int minutes = total / 60 % 60;
        int seconds = total % 60;

        // Six characters, one per digit: a part not in use is blank, the first part in use has no leading
        // zero, and the parts after it have two digits. E.g. 307 s is "   507", shown as " 5:07" with the
        // hours and their colon off.
        string text = hours > 0 ? $"{hours,2}{minutes:00}{seconds:00}"
                    : minutes > 0 ? $"  {minutes,2}{seconds:00}"
                    : $"    {seconds,2}";
        for (int i = 0; i < _digits.Length; i++)
        {
            _digits[i].Value = char.IsDigit(text[i]) ? text[i] - '0' : null;
        }
        _hoursColon.IsOn = hours > 0;
        _minutesColon.IsOn = hours > 0 || minutes > 0;
    }

    /// <summary>
    /// The largest time that fits: 99:59:59.
    /// </summary>
    private const int MaxSeconds = 99 * 3600 + 59 * 60 + 59;

    /// <summary>
    /// The six digits, left to right: hours, minutes, seconds (two each).
    /// </summary>
    private readonly SevenSegmentDigit[] _digits = new SevenSegmentDigit[6];

    /// <summary>
    /// The colon between the hours and the minutes.
    /// </summary>
    private readonly SevenSegmentColon _hoursColon;

    /// <summary>
    /// The colon between the minutes and the seconds.
    /// </summary>
    private readonly SevenSegmentColon _minutesColon;

    /// <summary>
    /// The row of digits and colons; the clock's child.
    /// </summary>
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
}
