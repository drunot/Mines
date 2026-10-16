using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Mines.Generators;

namespace Mines;

/// <summary>
/// The game: a toolbar with the counter, the face (reset) button, the settings button with its popup and
/// the timer, above the <see cref="Mines.BoardView"/>.
/// </summary>
/// <remarks>
/// <para>
/// Plays a finite board of <see cref="Rows"/> by <see cref="Columns"/>, or an endless board when
/// <see cref="Endless"/> is set. On a finite board the counter shows the bombs left and the game ends on a bomb or
/// a win; on an endless board it shows the safe clicks in a row (the best so far in its tooltip), and a bomb only
/// resets that count.
/// </para>
/// <para>
/// Builds a new board when the size or the settings change, and saves and loads the settings and an
/// unfinished finite game through <see cref="SavedState"/>.
/// </para>
/// </remarks>
public partial class Minefield : UserControl
{

    /// <summary>
    /// Defines the <see cref="Rows"/> property.
    /// </summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(Rows), defaultValue: 15);

    /// <summary>
    /// Defines the <see cref="Columns"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(Columns), defaultValue: 25);

    /// <summary>
    /// Defines the <see cref="BombCount"/> property.
    /// </summary>
    public static readonly StyledProperty<int> BombCountProperty =
        AvaloniaProperty.Register<Minefield, int>(nameof(BombCount), defaultValue: 75);

    /// <summary>
    /// The number of rows of the board. Changing it starts a new game.
    /// </summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>
    /// The number of columns of the board. Changing it starts a new game.
    /// </summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// The number of bombs on new boards. Takes effect with the next new game.
    /// </summary>
    public int BombCount
    {
        get => GetValue(BombCountProperty);
        set => SetValue(BombCountProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="Generator"/> property.
    /// </summary>
    public static readonly StyledProperty<GeneratorKind> GeneratorProperty =
        AvaloniaProperty.Register<Minefield, GeneratorKind>(nameof(Generator), defaultValue: GeneratorKind.NoGuessing);

    /// <summary>
    /// Which generator makes new boards.
    /// </summary>
    public GeneratorKind Generator
    {
        get => GetValue(GeneratorProperty);
        set => SetValue(GeneratorProperty, value);
    }

    /// <summary>
    /// Whether the game is played on an endless board instead of one of <see cref="Rows"/> by <see cref="Columns"/>.
    /// Takes effect with the next new board.
    /// </summary>
    public bool Endless { get; set; }

    /// <summary>
    /// The bomb density of an endless board, in percent. Takes effect with the next new board.
    /// </summary>
    public int DensityPercent { get; set; } = 15;

    /// <summary>
    /// Which generator makes endless boards: <see cref="GeneratorKind.NoGuessing"/> keeps the first click's
    /// area free and tries to keep every area solvable by logic; <see cref="GeneratorKind.Classic"/> only keeps the
    /// clicked cell free. Takes effect with the next new board.
    /// </summary>
    public GeneratorKind EndlessGenerator { get; set; } = GeneratorKind.NoGuessing;

    /// <summary>
    /// Raised when a finite board of a different size than before (or after an endless board) starts, so the
    /// window can fit itself to it. Not raised for a new game of the same size, or for an endless board.
    /// </summary>
    public event EventHandler? BoardSizeChanged;

    /// <summary>
    /// Raised whenever a new board is shown (a new game or a restored one), after the toolbar shows it.
    /// The toolbar's width can change with it (an endless board has two counters on the left).
    /// </summary>
    public event EventHandler? BoardShown;

    /// <summary>
    /// The size this control needs to show the whole finite board with cells of
    /// <see cref="BoardView.DefaultCellMillimetres"/> mm: the board plus the toolbar and frames around it, and at
    /// least as wide as the toolbar. <c>null</c> for an endless board, or before the first layout.
    /// </summary>
    public Size? PreferredSize
    {
        get
        {
            if (BoardView.PreferredSize is not Size board || BoardScroller.Bounds.Width <= 0)
            {
                return null;
            }
            // Everything that isn't the board: toolbar, frames and margins.
            Size chrome = new Size(Bounds.Width - BoardScroller.Bounds.Width, Bounds.Height - BoardScroller.Bounds.Height);
            return new Size(Math.Max(chrome.Width + board.Width, MinimumWidth), chrome.Height + board.Height);
        }
    }

    /// <summary>
    /// The narrowest this control can be without squeezing the toolbar: the face in the middle with room for the
    /// wider of the two sides on both sides (the side columns share the space equally), plus the margins.
    /// </summary>
    public double MinimumWidth
    {
        get
        {
            double side = Math.Max(LeftCounters.DesiredSize.Width, RightTools.DesiredSize.Width);
            double margins = Toolbar.Margin.Left + Toolbar.Margin.Right + Toolbar.Padding.Left + Toolbar.Padding.Right
                           + ToolbarGrid.Margin.Left + ToolbarGrid.Margin.Right;
            return Math.Ceiling(2 * side + ResetButton.DesiredSize.Width + margins);
        }
    }

    /// <summary>
    /// The window's content size, remembered with the settings so the next start opens at the same size.
    /// <c>null</c> until the window has been shown (or on the very first start).
    /// </summary>
    public Size? WindowSize { get; set; }

    /// <summary>
    /// Creates the control, builds the first board and wires up the toolbar and the settings popup.
    /// </summary>
    public Minefield()
    {
        InitializeComponent();

        _timer.Tick += (sender, e) => ShowTime();
        BuildBoard();
        BoardView.PressingChanged += (sender, e) => UpdateFace();
        ResetButton.Click += (sender, e) => BuildBoard();

        SettingsButton.Click += (sender, e) =>
        {
            SettingsPanel.Load(Rows, Columns, BombCount, Generator, Endless, DensityPercent, EndlessGenerator);
            SettingsPopup.IsOpen = true;
        };
        SettingsPanel.Cancel += (sender, e) => SettingsPopup.IsOpen = false;
        SettingsPanel.NewGame += (sender, e) =>
        {
            SettingsPopup.IsOpen = false;
            _applying_settings = true;   // one new board for all the changes, not one per property
            Rows = SettingsPanel.Rows;
            Columns = SettingsPanel.Columns;
            BombCount = SettingsPanel.BombCount;
            Generator = SettingsPanel.Generator;
            Endless = SettingsPanel.Endless;
            DensityPercent = SettingsPanel.DensityPercent;
            EndlessGenerator = SettingsPanel.EndlessGenerator;
            _applying_settings = false;
            BuildBoard();
            SaveState();   // remember the new settings right away
        };
    }

    /// <summary>
    /// Builds a new board when <see cref="Rows"/> or <see cref="Columns"/> changes, except while several
    /// settings are being applied at once.
    /// </summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!_applying_settings && (change.Property == RowsProperty || change.Property == ColumnsProperty))
        {
            BuildBoard();
        }
    }


    /// <summary>
    /// A new game: a fresh board, shown by the board view.
    /// </summary>
    /// <remarks>
    /// An endless board with <see cref="DensityPercent"/> and <see cref="EndlessGenerator"/>, or a finite one made by the generator chosen by
    /// <see cref="Generator"/>. Unlocks the board and resets the timer, the counter and the face.
    /// </remarks>
    [MemberNotNull(nameof(_board))]
    private void BuildBoard()
    {
        if (Endless)
        {
            GeneratorType type = EndlessGenerator == GeneratorKind.Classic ? GeneratorType.Random : GeneratorType.Deterministic;
            _endless_density = DensityPercent;
            _endless_generator = EndlessGenerator;
            ShowBoard(new InfiniteFieldMatrix(DensityPercent / 100f, type));
            return;
        }
        IMatrixGenerator generator = Generator == GeneratorKind.Classic
            ? new ClassicRandomGenerator()
            : new FastDeterministicGenerator();
        ShowBoard(new FieldMatrix(Rows, Columns, BombCount, generator));
    }

    /// <summary>
    /// Makes <paramref name="board"/> the current board: listens to it, shows it, unlocks it, and resets the
    /// timer, the counter and the face. Raises <see cref="BoardSizeChanged"/> if its size differs from before.
    /// </summary>
    /// <param name="board">The new board.</param>
    [MemberNotNull(nameof(_board))]
    private void ShowBoard(IBoard board)
    {
        if (_board is not null)
        {
            _board.FieldChanged -= OnBoardChanged;   // stop listening to the old board
        }
        _board = board;
        _board.FieldChanged += OnBoardChanged;
        BoardView.Board = _board;
        BoardView.IsLocked = false;
        ResetTimer();
        UpdateCounter();
        UpdateFace();

        BoardShown?.Invoke(this, EventArgs.Empty);
        if (_board.Size != _shown_size)
        {
            _shown_size = _board.Size;
            if (_shown_size is not null)
            {
                BoardSizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// The size of the board shown last (<c>null</c> for an endless board), to tell a new size from a new game.
    /// </summary>
    private BoardSize? _shown_size;

    /// <summary>
    /// Handles a change of a cell on the board: updates the bomb counter, starts the clock on the first
    /// reveal and schedules a face update.
    /// </summary>
    /// <param name="sender">The board.</param>
    /// <param name="e">The event data.</param>
    private void OnBoardChanged(object? sender, EventArgs e)
    {
        UpdateCounter();
        // The first click places the bombs and reveals; that starts the clock (a flag before it doesn't).
        if (!_clock.IsRunning && !_time_final && _board.Initialized && !_board.GameLost)
        {
            _clock.Start();
            _timer.Start();
            ShowTime();
        }
        // A cascade raises FieldChanged once per revealed cell, and checking for a win looks at every cell,
        // so the face is updated once, after the current batch of changes.
        if (!_face_update_pending)
        {
            _face_update_pending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _face_update_pending = false;
                UpdateFace();
            });
        }
    }

    /// <summary>
    /// True while a face update has been posted to the dispatcher but has not run yet.
    /// </summary>
    private bool _face_update_pending;
    /// <summary>
    /// True while several settings are being changed together, so that only one new board is built.
    /// </summary>
    private bool _applying_settings;

    /// <summary>
    /// Updates the face on the reset button and handles the end of a game.
    /// </summary>
    /// <remarks>
    /// Dead if lost, sunglasses if won, excited while a button is held down on the board, otherwise normal.
    /// A lost or won game also stops the clock and locks the board; a won game flags the remaining bombs.
    /// An endless board never ends: it shows the dead face from hitting a bomb until the next safe click.
    /// </remarks>
    private void UpdateFace()
    {
        bool lost = _board.GameLost;
        bool won = !lost && _board.GameWon;
        bool just_hit = _board is InfiniteFieldMatrix endless && endless.LastActionHitBomb;
        if ((lost || won) && _clock.IsRunning)
        {
            _clock.Stop();
            _timer.Stop();
            _time_final = true;   // stays stopped until the next game
            ShowTime();
        }
        if (won && _board is FieldMatrix finished)
        {
            // Every safe cell is revealed, so the remaining hidden cells are the bombs: flag them all.
            foreach (Field field in finished.Fields)
            {
                if (field.IsBomb && !field.Flagged)
                {
                    field.Flagged = true;
                }
            }
        }
        if (lost || won)
        {
            BoardView.IsLocked = true;   // the game is over; a new game unlocks the board
        }
        ResetButton.Face = lost || just_hit ? Face.Dead
                         : won ? Face.Won
                         : BoardView.IsPressing ? Face.Excited
                         : Face.Normal;
    }

    // ---- Time: starts on the first reveal, stops when the game is lost or won, resets with the board ----

    /// <summary>
    /// Measures the playing time of the current game (since this run of the app).
    /// </summary>
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };   // refreshes the display
    /// <summary>
    /// True once the game is lost or won: the time is final and the clock must not start again until the next game.
    /// </summary>
    private bool _time_final;

    private TimeSpan _time_before;   // time from before a restart, when an unfinished game is continued

    /// <summary>
    /// The total playing time of the current game, including time from before a restart.
    /// </summary>
    private TimeSpan Elapsed => _time_before + _clock.Elapsed;

    /// <summary>
    /// Stops and clears the clock for a new game and shows zero.
    /// </summary>
    private void ResetTimer()
    {
        _clock.Reset();
        _timer.Stop();
        _time_before = TimeSpan.Zero;
        _time_final = false;
        ShowTime();
    }

    /// <summary>
    /// Shows the elapsed time, in whole seconds, on the clock (hh:mm:ss).
    /// </summary>
    private void ShowTime() => TimeCounter.Seconds = (int)Math.Min(int.MaxValue, Elapsed.TotalSeconds);

    // ---- Saving and loading (SavedState): the settings, and the board if a game was left unfinished ----

    /// <summary>
    /// Saves the settings and the game: a finite game only if it was started and is neither lost nor won, an
    /// endless game whenever it was started (with where the view was).
    /// </summary>
    public void SaveState()
    {
        bool unfinished = _board is FieldMatrix matrix && matrix.Initialized && !matrix.GameLost && !matrix.GameWon;
        new SavedState
        {
            Settings = new SavedSettings
            {
                Rows = Rows,
                Columns = Columns,
                BombCount = BombCount,
                Generator = Generator,
                Endless = Endless,
                DensityPercent = DensityPercent,
                EndlessGenerator = EndlessGenerator,
                WindowWidth = WindowSize?.Width,
                WindowHeight = WindowSize?.Height,
            },
            Game = unfinished ? SavedGame.From((FieldMatrix)_board, Elapsed) : null,
            EndlessGame = _board is InfiniteFieldMatrix endless && endless.Initialized
                ? new SavedEndlessGame
                {
                    DensityPercent = _endless_density,
                    Generator = _endless_generator,
                    ElapsedSeconds = Elapsed.TotalSeconds,
                    ViewRow = BoardView.CenterCell.X,
                    ViewColumn = BoardView.CenterCell.Y,
                    ViewZoom = BoardView.ZoomFactor,
                    Board = endless.Save(),
                }
                : null,
        }.Save();
    }

    /// <summary>
    /// Loads the saved settings and continues a saved unfinished game (timer included).
    /// </summary>
    /// <remarks>
    /// Without a save file, the current settings and board stay. With settings but no valid saved game,
    /// a new board is built.
    /// </remarks>
    public void LoadState()
    {
        SavedState? state = SavedState.Load();
        if (state is null)
        {
            return;
        }
        _applying_settings = true;   // one new board for all the changes, not one per property
        Rows = state.Settings.Rows;
        Columns = state.Settings.Columns;
        BombCount = state.Settings.BombCount;
        Generator = state.Settings.Generator;
        Endless = state.Settings.Endless;
        DensityPercent = state.Settings.DensityPercent;
        EndlessGenerator = state.Settings.EndlessGenerator;
        if (state.Settings.WindowWidth is double width && state.Settings.WindowHeight is double height
            && width > 0 && height > 0)
        {
            WindowSize = new Size(width, height);
        }
        _applying_settings = false;

        if (!Endless && state.Game is SavedGame game && game.IsValid())
        {
            ContinueGame(game);
        }
        else if (Endless && state.EndlessGame is SavedEndlessGame endless && endless.ToBoard() is InfiniteFieldMatrix board)
        {
            ContinueEndlessGame(endless, board);
        }
        else
        {
            BuildBoard();
        }
    }

    /// <summary>
    /// Replaces the current board with a saved unfinished game and keeps its clock running from the saved time.
    /// </summary>
    /// <param name="game">The saved game; it should have passed <see cref="SavedGame.IsValid"/>.</param>
    private void ContinueGame(SavedGame game)
    {
        ShowBoard(game.ToBoard());   // restored before listening, so restoring doesn't count as moves

        _time_before = TimeSpan.FromSeconds(game.ElapsedSeconds);
        _clock.Start();   // the game was running, so the timer continues
        _timer.Start();
        ShowTime();
    }

    /// <summary>
    /// Continues a saved endless game: its board, its time and its view.
    /// </summary>
    /// <param name="game">The saved game.</param>
    /// <param name="board">Its board, restored with <see cref="SavedEndlessGame.ToBoard"/>.</param>
    private void ContinueEndlessGame(SavedEndlessGame game, InfiniteFieldMatrix board)
    {
        _endless_density = game.DensityPercent;
        _endless_generator = game.Generator;
        ShowBoard(board);
        BoardView.ShowCell(game.ViewRow, game.ViewColumn, game.ViewZoom);

        _time_before = TimeSpan.FromSeconds(game.ElapsedSeconds);
        _clock.Start();   // the game was running, so the timer continues
        _timer.Start();
        ShowTime();
    }

    /// <summary>
    /// The density and generator of the endless board shown, which may differ from <see cref="DensityPercent"/>
    /// and <see cref="EndlessGenerator"/> once the settings change; saved with the game.
    /// </summary>
    private int _endless_density = 15;
    private GeneratorKind _endless_generator = GeneratorKind.NoGuessing;

    /// <summary>
    /// Shows the counters on the left. A finite board: the bombs left (bombs minus flags). An endless board:
    /// the safe clicks in a row, and next to it the best streak of this game.
    /// </summary>
    private void UpdateCounter()
    {
        if (_board is InfiniteFieldMatrix endless)
        {
            BombsLeftCounter.Value = endless.Streak;
            BestCounter.Value = endless.BestStreak;
            BestCounter.IsVisible = true;
            ToolTip.SetTip(BombsLeftCounter, $"Safe clicks in a row (bombs hit: {endless.BombsHit})");
            ToolTip.SetTip(BestCounter, "Best streak in this game");
        }
        else if (_board is FieldMatrix matrix)
        {
            BombsLeftCounter.Value = matrix.BombsLeft;
            BestCounter.IsVisible = false;
            ToolTip.SetTip(BombsLeftCounter, "Bombs left");
        }
    }

    /// <summary>
    /// The board of the current game: a <see cref="FieldMatrix"/> or an <see cref="InfiniteFieldMatrix"/>.
    /// </summary>
    private IBoard _board;
}