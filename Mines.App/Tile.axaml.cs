using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;

using System;
using System.Linq;

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
            Field.Parent.Initialize(Field.Position);
            if (!Field.Hidden)
            {
                if(Field.Number == Field.Neighbors.Count((Field f) => f.Flagged))
                {
                    foreach(Field field in Field.Neighbors)
                    {
                        if (!field.Flagged)
                        {
                            field.Hidden = false;
                        }
                    }
                }
            }
            Field.Hidden = false;
        }
    }

    bool questioned = false;

    private void OnTilePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right && Field is not null)
        {
            if (questioned)
            {
                questioned = false;
                StyleButton();
                return;
            }

            if (Field.Flagged)
            {
                questioned = true;
            }
            Field.Flagged = !Field.Flagged;
        }
    }

    // Shows the field's state by switching parts of the tile on and off; the tile's content stays the same.
    private void StyleButton()
    {
        if (Field is null)
        {
            return;
        }

        bool revealed = !Field.Hidden;
        TileButton.Classes.Set("revealed", revealed);   // flat, no bevel
        FlagIcon.IsVisible = Field.Flagged;
        BombIcon.IsVisible = revealed && Field.IsBomb;

        NumberText.IsVisible = revealed && Field.Number is not null;
        if (NumberText.IsVisible)
        {
            NumberText.Text = Field.Number.ToString();
            NumberText.Bind(TextBlock.ForegroundProperty, NumberText.GetResourceObservable($"NumberBrush{Field.Number}"));
        }

        string background = !revealed ? "HiddenBrush" : Field.IsBomb ? "BombBrush" : "ClearBrush";
        TileButton.Bind(BackgroundProperty, TileButton.GetResourceObservable(background));
        QuestionIcon.IsVisible = !revealed && questioned;
    }

}