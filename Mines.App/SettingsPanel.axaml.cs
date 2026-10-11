using System;
using Avalonia.Controls;

namespace Mines;

// Which generator makes the boards.
public enum GeneratorKind
{
    NoGuessing,   // DeterministicGenerator: always solvable by logic
    Classic,      // ClassicRandomGenerator: random, may need a guess
}

// The settings shown in the popup: board size, bomb count and generator.
// Load() fills it in; NewGame or Cancel is raised when the player is done.
public partial class SettingsPanel : UserControl
{
    // The player chose "New game": read Rows, Columns, BombCount and Generator.
    public event EventHandler? NewGame;

    public event EventHandler? Cancel;

    public SettingsPanel()
    {
        InitializeComponent();
        RowsInput.ValueChanged += (sender, e) => LimitBombs();
        ColumnsInput.ValueChanged += (sender, e) => LimitBombs();
        NoGuessingOption.IsCheckedChanged += (sender, e) => LimitBombs();
        NewGameButton.Click += (sender, e) => NewGame?.Invoke(this, EventArgs.Empty);
        CancelButton.Click += (sender, e) => Cancel?.Invoke(this, EventArgs.Empty);
    }

    public int Rows => (int)(RowsInput.Value ?? RowsInput.Minimum);
    public int Columns => (int)(ColumnsInput.Value ?? ColumnsInput.Minimum);
    public int BombCount => (int)(BombsInput.Value ?? BombsInput.Minimum);
    public GeneratorKind Generator => ClassicOption.IsChecked == true ? GeneratorKind.Classic : GeneratorKind.NoGuessing;

    public void Load(int rows, int columns, int bomb_count, GeneratorKind generator)
    {
        RowsInput.Value = rows;
        ColumnsInput.Value = columns;
        NoGuessingOption.IsChecked = generator == GeneratorKind.NoGuessing;
        ClassicOption.IsChecked = generator == GeneratorKind.Classic;
        LimitBombs();
        BombsInput.Value = Math.Min(bomb_count, BombsInput.Maximum);
    }

    // The generators keep the first clicked cell free of bombs: the classic one just that cell, the
    // no-guessing one the cell and its neighbours (up to 9 cells). More bombs than that won't fit.
    private void LimitBombs()
    {
        int free = Generator == GeneratorKind.Classic ? 1 : 9;
        BombsInput.Maximum = Math.Max(BombsInput.Minimum, Rows * Columns - free);
        if (BombsInput.Value > BombsInput.Maximum)
        {
            BombsInput.Value = BombsInput.Maximum;
        }
    }
}
