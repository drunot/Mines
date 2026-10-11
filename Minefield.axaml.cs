using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Microsoft.VisualBasic;
using Mines.Solving;
using Mines.Generators;

namespace Mines;

public partial class Minefield : UserControl
{

    private ISolver _solver = new ConstraintSolver();
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(Rows), defaultValue: 9);

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(Columns), defaultValue: 9);

    public static readonly StyledProperty<int> BombCountProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(BombCount), defaultValue: 75);

    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int BombCount
    {
        get => GetValue(BombCountProperty);
        set => SetValue(BombCountProperty, value);
    }

    public Minefield()
    {
        InitializeComponent();

        _fieldMatrix = new FieldMatrix(Rows, Columns, BombCount, new DeterministicGenerator());
        BuildBoard();
        ResetButton.Click += (sender, e) =>
        {
            if (int.TryParse(BombTextBox.Text, out int bomb_count))
            {
                BombCount = bomb_count;
            }
            BuildBoard();
        };
        BombTextBox.Text = BombCount.ToString();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RowsProperty || change.Property == ColumnsProperty)
        {
            BuildBoard();
        }
    }


    private void BuildBoard()
    {
        _fieldMatrix = new FieldMatrix(Rows, Columns, BombCount, new DeterministicGenerator());
        BuildBoard(BoardGrid, _fieldMatrix);
    }

    private void BuildBoard(Grid boardGrid, FieldMatrix fieldMatrix)
    {


        boardGrid.Children.Clear();
        boardGrid.RowDefinitions.Clear();
        boardGrid.ColumnDefinitions.Clear();


        for (int r = 0; r < Rows + 1; r++)
        {
            boardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }


        for (int r = 0; r < Rows + 1; r++)
        {
            boardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (r == 0) continue;
            var text = new TextBlock
            {
                Text = r.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(text, r);
            Grid.SetColumn(text, 0);
            boardGrid.Children.Add(text);
        }

        for (int c = 0; c < Columns + 1; c++)
        {
            boardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            if (c == 0) continue;
            var text = new TextBlock
            {
                Text = System.Convert.ToChar(c + 96).ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(text, 0);
            Grid.SetColumn(text, c);
            boardGrid.Children.Add(text);
        }

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                var tile = new Tile { Field = fieldMatrix[r, c] };

                Grid.SetRow(tile, r + 1);
                Grid.SetColumn(tile, c + 1);
                boardGrid.Children.Add(tile);
            }
        }
    }

    private FieldMatrix _fieldMatrix;
}