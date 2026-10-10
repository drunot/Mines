using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;

using System;

namespace Mines;

public partial class Tile : UserControl
{
    public static readonly StyledProperty<Field?> FieldProperty =
        AvaloniaProperty.Register<Tile, Field?>(nameof(Field), defaultValue: null);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldProperty)
        {
            if (change.OldValue is Field old_field)
            {
                old_field.FieldChanged -= OnFieldHiddenChanged;
            }
            if (change.NewValue is Field new_field)
            {
                new_field.FieldChanged += OnFieldHiddenChanged;
            }
            StyleButton();
        }
    }

    private void OnFieldHiddenChanged(object? sender, EventArgs e) => StyleButton();

    public Field? Field
    {
        get => GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }


    public Tile()
    {
        InitializeComponent();
        TileButton.Click += OnTileClick;
        TileButton.PointerReleased += OnTilePointerReleased;
    }

    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (Field is not null)
        {
            Field.Hidden = false;
        }
    }

    private void OnTilePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right && Field is not null)
        {
            Field.Flagged = !Field.Flagged;
        }
    }

    private void StyleButton()
    {
        if (Field is null)
        {
            return;
        }
        if (Field.Flagged)
        {
            TileButton.Bind(BackgroundProperty, TileButton.GetResourceObservable("FlagBrush"));
            return;
        }
        if (Field.Hidden)
        {
            TileButton.Bind(BackgroundProperty, TileButton.GetResourceObservable("HiddenBrush"));
            return;
        }

        if (Field.IsBomb)
        {
            TileButton.Bind(BackgroundProperty, TileButton.GetResourceObservable("BombBrush"));
        }
        else
        {
            TileButton.ClearValue(BackgroundProperty);
        }

        TileButton.Content = new TextBlock
        {
            Text = Field.Number.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

}