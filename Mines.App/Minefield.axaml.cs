using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Mines.Generators;

namespace Mines;

/// <summary>
/// The game: a toolbar with the bomb counter, the face (reset) button, the settings button with its popup and
/// the timer, above the <see cref="Mines.BoardView"/>.
/// </summary>
/// <remarks>
/// Builds a new board when the size or the settings change, and saves and loads the settings and an
/// unfinished game through <see cref="SavedState"/>.
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
            SettingsPanel.Load(Rows, Columns, BombCount, Generator);
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
    /// Uses the generator chosen by <see cref="Generator"/>, unlocks the board and resets the timer,
    /// the bomb counter and the face.
    /// </remarks>
    [MemberNotNull(nameof(_fieldMatrix))]
    private void BuildBoard()
    {
        if (_fieldMatrix is not null)
        {
            _fieldMatrix.FieldChanged -= OnBoardChanged;   // stop listening to the old board
        }
        IMatrixGenerator generator = Generator == GeneratorKind.Classic
            ? new ClassicRandomGenerator()
            : new FastDeterministicGenerator();
        _fieldMatrix = new FieldMatrix(Rows, Columns, BombCount, generator);
        _fieldMatrix.FieldChanged += OnBoardChanged;
        BoardView.Board = _fieldMatrix;
        BoardView.IsLocked = false;
        ResetTimer();
        UpdateBombsLeft();
        UpdateFace();
    }

    /// <summary>
    /// Handles a change of a cell on the board: updates the bomb counter, starts the clock on the first
    /// reveal and schedules a face update.
    /// </summary>
    /// <param name="sender">The board.</param>
    /// <param name="e">The event data.</param>
    private void OnBoardChanged(object? sender, EventArgs e)
    {
        UpdateBombsLeft();
        // The first click places the bombs and reveals; that starts the clock (a flag before it doesn't).
        if (!_clock.IsRunning && !_time_final && _fieldMatrix.Initialized && !_fieldMatrix.GameLost)
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
    /// </remarks>
    private void UpdateFace()
    {
        bool lost = _fieldMatrix.GameLost;
        bool won = !lost && _fieldMatrix.GameWon;
        if ((lost || won) && _clock.IsRunning)
        {
            _clock.Stop();
            _timer.Stop();
            _time_final = true;   // stays stopped until the next game
            ShowTime();
        }
        if (won)
        {
            // Every safe cell is revealed, so the remaining hidden cells are the bombs: flag them all.
            foreach (Field field in _fieldMatrix.Fields)
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
        ResetButton.Face = lost ? Face.Dead
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
    /// Saves the settings, and the board only if the game was started and is neither lost nor won.
    /// </summary>
    public void SaveState()
    {
        bool unfinished = _fieldMatrix.Initialized && !_fieldMatrix.GameLost && !_fieldMatrix.GameWon;
        new SavedState
        {
            Settings = new SavedSettings { Rows = Rows, Columns = Columns, BombCount = BombCount, Generator = Generator },
            Game = unfinished ? SavedGame.From(_fieldMatrix, BoardView.QuestionMarks, Elapsed) : null,
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
        _applying_settings = false;

        if (state.Game is SavedGame game && game.IsValid())
        {
            ContinueGame(game);
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
        _fieldMatrix.FieldChanged -= OnBoardChanged;
        _fieldMatrix = game.ToBoard();
        _fieldMatrix.FieldChanged += OnBoardChanged;   // after restoring, so restoring doesn't count as moves
        BoardView.Board = _fieldMatrix;
        BoardView.SetQuestionMarks(game.Questioned);
        BoardView.IsLocked = false;

        ResetTimer();
        _time_before = TimeSpan.FromSeconds(game.ElapsedSeconds);
        _clock.Start();   // the game was running, so the timer continues
        _timer.Start();
        ShowTime();
        UpdateBombsLeft();
        UpdateFace();
    }
    /// <summary>
    /// Shows the number of bombs left (bombs minus flags) on the bomb counter.
    /// </summary>
    private void UpdateBombsLeft() => BombsLeftCounter.Value = _fieldMatrix.BombsLeft;


    /// <summary>
    /// The board of the current game.
    /// </summary>
    private FieldMatrix _fieldMatrix;
}