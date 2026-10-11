using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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

    public static readonly StyledProperty<GeneratorKind> GeneratorProperty =
        AvaloniaProperty.Register<Minefield, GeneratorKind>(nameof(Generator), defaultValue: GeneratorKind.NoGuessing);

    // Which generator makes new boards.
    public GeneratorKind Generator
    {
        get => GetValue(GeneratorProperty);
        set => SetValue(GeneratorProperty, value);
    }

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!_applying_settings && (change.Property == RowsProperty || change.Property == ColumnsProperty))
        {
            BuildBoard();
        }
    }


    // A new game: a fresh board, shown by the board view.
    [MemberNotNull(nameof(_fieldMatrix))]
    private void BuildBoard()
    {
        if (_fieldMatrix is not null)
        {
            _fieldMatrix.FieldChanged -= OnBoardChanged;   // stop listening to the old board
        }
        IMatrixGenerator generator = Generator == GeneratorKind.Classic
            ? new ClassicRandomGenerator()
            : new DeterministicGenerator();
        _fieldMatrix = new FieldMatrix(Rows, Columns, BombCount, generator);
        _fieldMatrix.FieldChanged += OnBoardChanged;
        BoardView.Board = _fieldMatrix;
        BoardView.IsLocked = false;
        ResetTimer();
        UpdateBombsLeft();
        UpdateFace();
    }

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

    private bool _face_update_pending;
    private bool _applying_settings;

    // Dead if lost, sunglasses if won, excited while a button is held down on the board, otherwise normal.
    // A lost or won game also stops the clock and locks the board; a won game flags the remaining bombs.
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

    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };   // refreshes the display
    private bool _time_final;

    private TimeSpan _time_before;   // time from before a restart, when an unfinished game is continued

    private TimeSpan Elapsed => _time_before + _clock.Elapsed;

    private void ResetTimer()
    {
        _clock.Reset();
        _timer.Stop();
        _time_before = TimeSpan.Zero;
        _time_final = false;
        ShowTime();
    }

    private void ShowTime() => TimeCounter.Value = (int)Math.Min(int.MaxValue, Elapsed.TotalSeconds);

    // ---- Saving and loading (SavedState): the settings, and the board if a game was left unfinished ----

    // Saves the settings, and the board only if the game was started and is neither lost nor won.
    public void SaveState()
    {
        bool unfinished = _fieldMatrix.Initialized && !_fieldMatrix.GameLost && !_fieldMatrix.GameWon;
        new SavedState
        {
            Settings = new SavedSettings { Rows = Rows, Columns = Columns, BombCount = BombCount, Generator = Generator },
            Game = unfinished ? SavedGame.From(_fieldMatrix, BoardView.QuestionMarks, Elapsed) : null,
        }.Save();
    }

    // Loads the saved settings and continues a saved unfinished game (timer included). Without a save file,
    // the current settings and board stay.
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
    private void UpdateBombsLeft() => BombsLeftCounter.Value = _fieldMatrix.BombsLeft;


    private FieldMatrix _fieldMatrix;
}