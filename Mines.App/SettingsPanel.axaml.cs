using System;
using Avalonia.Controls;

namespace Mines;

/// <summary>
/// Which generator makes the boards, for a finite or an endless board.
/// </summary>
public enum GeneratorKind
{
    /// <summary>
    /// A finite board uses <c>FastDeterministicGenerator</c>: always solvable by logic. An endless board uses
    /// <see cref="GeneratorType.Deterministic"/>: the first click opens an area, and the board tries to keep every
    /// area solvable by logic.
    /// </summary>
    NoGuessing,
    /// <summary>
    /// A finite board uses <c>ClassicRandomGenerator</c>, an endless board <see cref="GeneratorType.Random"/>:
    /// random bombs that may need a guess; only the first clicked cell is kept free.
    /// </summary>
    Classic,
}

/// <summary>
/// The settings shown in the popup: a "Board" tab (size, bomb count and generator) and an "Endless" tab (bomb
/// density and generator). The tab that is open when "New game" is pressed decides which kind of game starts.
/// </summary>
/// <remarks>
/// <see cref="Load"/> fills it in; <see cref="NewGame"/> or <see cref="Cancel"/> is raised when the player is done.
/// </remarks>
public partial class SettingsPanel : UserControl
{
    /// <summary>
    /// The player chose "New game": read <see cref="Endless"/>, and then <see cref="Rows"/>,
    /// <see cref="Columns"/>, <see cref="BombCount"/> and <see cref="Generator"/>, or <see cref="DensityPercent"/>
    /// and <see cref="EndlessGenerator"/>.
    /// </summary>
    public event EventHandler? NewGame;

    /// <summary>
    /// The player cancelled; the settings should be left unchanged.
    /// </summary>
    public event EventHandler? Cancel;

    /// <summary>
    /// Creates the panel and wires up its inputs and buttons.
    /// </summary>
    public SettingsPanel()
    {
        InitializeComponent();
        RowsInput.ValueChanged += (sender, e) => LimitBombs();
        ColumnsInput.ValueChanged += (sender, e) => LimitBombs();
        NoGuessingOption.IsCheckedChanged += (sender, e) => LimitBombs();
        NewGameButton.Click += (sender, e) => NewGame?.Invoke(this, EventArgs.Empty);
        CancelButton.Click += (sender, e) => Cancel?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The number of rows entered, or the input's minimum if it is empty.
    /// </summary>
    public int Rows => (int)(RowsInput.Value ?? RowsInput.Minimum);
    /// <summary>
    /// The number of columns entered, or the input's minimum if it is empty.
    /// </summary>
    public int Columns => (int)(ColumnsInput.Value ?? ColumnsInput.Minimum);
    /// <summary>
    /// The number of bombs entered, or the input's minimum if it is empty.
    /// </summary>
    public int BombCount => (int)(BombsInput.Value ?? BombsInput.Minimum);
    /// <summary>
    /// The selected generator: <see cref="GeneratorKind.Classic"/> if that option is checked,
    /// otherwise <see cref="GeneratorKind.NoGuessing"/>.
    /// </summary>
    public GeneratorKind Generator =>
        ClassicOption.IsChecked == true ? GeneratorKind.Classic : GeneratorKind.NoGuessing;
    /// <summary>
    /// Whether the "Endless" tab is selected: start an infinite board instead of a finite one.
    /// </summary>
    public bool Endless => ModeTabs.SelectedIndex == 1;
    /// <summary>
    /// The bomb density for an endless board, in percent, or the input's minimum if it is empty.
    /// </summary>
    public int DensityPercent => (int)(DensityInput.Value ?? DensityInput.Minimum);
    /// <summary>
    /// The generator selected for an endless board: <see cref="GeneratorKind.Classic"/> if that option is
    /// checked, otherwise <see cref="GeneratorKind.NoGuessing"/>.
    /// </summary>
    public GeneratorKind EndlessGenerator =>
        EndlessClassicOption.IsChecked == true ? GeneratorKind.Classic : GeneratorKind.NoGuessing;

    /// <summary>
    /// Fills the inputs with the given settings and opens the tab of the current kind of game. The bomb count
    /// is limited to what fits on the board.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="bombCount">The number of bombs.</param>
    /// <param name="generator">The generator to select.</param>
    /// <param name="endless">Whether the current game is endless (opens the "Endless" tab).</param>
    /// <param name="densityPercent">The bomb density for an endless board, in percent.</param>
    /// <param name="endlessGenerator">The generator to select for an endless board.</param>
    public void Load(int rows, int columns, int bombCount, GeneratorKind generator, bool endless, int densityPercent,
                     GeneratorKind endlessGenerator)
    {
        EndlessNoGuessingOption.IsChecked = endlessGenerator == GeneratorKind.NoGuessing;
        EndlessClassicOption.IsChecked = endlessGenerator == GeneratorKind.Classic;
        ModeTabs.SelectedIndex = endless ? 1 : 0;
        DensityInput.Value = Math.Clamp(densityPercent, (int)DensityInput.Minimum, (int)DensityInput.Maximum);
        RowsInput.Value = rows;
        ColumnsInput.Value = columns;
        NoGuessingOption.IsChecked = generator == GeneratorKind.NoGuessing;
        ClassicOption.IsChecked = generator == GeneratorKind.Classic;
        LimitBombs();
        BombsInput.Value = Math.Min(bombCount, BombsInput.Maximum);
    }

    /// <summary>
    /// Limits the bomb count input to the number of bombs that fit on the board.
    /// </summary>
    /// <remarks>
    /// The generators keep the first clicked cell free of bombs: the classic one just that cell, the
    /// no-guessing one the cell and its neighbours (up to 9 cells). More bombs than that won't fit.
    /// </remarks>
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
