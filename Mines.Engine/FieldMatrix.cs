using System;
using System.Linq;
using Mines.Generators;

namespace Mines;


/// <summary>
/// Receives notifications from a <see cref="Field"/> when it is revealed or its flag changes.
/// </summary>
public interface IFieldOwner
{
    /// <summary>
    /// Called after the <see cref="Field.Hidden"/> state of <paramref name="field"/> changed.
    /// </summary>
    /// <param name="field">The field whose hidden state changed.</param>
    void OnRevealed(Field field);
    /// <summary>
    /// Called after the flag of a field changed.
    /// </summary>
    /// <param name="flagged">The new flag state.</param>
    void OnFlagChanged(bool flagged);
}
/// <summary>
/// The Minesweeper board: a grid of <see cref="Field"/>s indexed by row and column, plus the game
/// state (bombs left, won/lost).
/// </summary>
public class FieldMatrix
{

    /// <summary>
    /// The part of <see cref="FieldMatrix"/> its fields may use. <see cref="FieldMatrix"/> gives it only to
    /// its own fields.
    /// </summary>
    /// <remarks>
    /// Keeps the counters up to date, reveals the neighbours of fields without neighbouring bombs
    /// (cascade), and reveals all unflagged bombs when a bomb is revealed.
    /// </remarks>
    class FieldOwnerHandler : IFieldOwner
    {

        /// <summary>
        /// Creates a handler for <paramref name="matrix"/>.
        /// </summary>
        /// <param name="matrix">The board whose counters and state this handler updates.</param>
        public FieldOwnerHandler(FieldMatrix matrix)
        {
            _matrix = matrix;
        }

        /// <inheritdoc/>
        public void OnRevealed(Field field)
        {
            _matrix._fields_left += field.Hidden ? 1 : -1;   // the field can also be hidden again
            if (field.Hidden)
            {
                _matrix.FieldChanged?.Invoke(_matrix, EventArgs.Empty);
                return;
            }

            if (!field.IsBomb && field.Number == null)
            {
                foreach (Field f in field.Neighbors)
                {
                    f.Hidden = false;
                }
                return;
            }
            if (field.IsBomb)
            {
                foreach (Field grid_field in _matrix.Fields)
                {
                    if (grid_field.IsBomb && !grid_field.Flagged)
                    {
                        grid_field.Hidden = false;
                    }
                }
                _matrix._game_lost = true;
            }
            _matrix.FieldChanged?.Invoke(_matrix, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void OnFlagChanged(bool flagged)
        {

            if (flagged == true)
            {
                _matrix._fields_left -= 1;
                _matrix._bombs_left -= 1;
            }
            else
            {
                _matrix._fields_left += 1;
                _matrix._bombs_left += 1;
            }
            _matrix.FieldChanged?.Invoke(_matrix, EventArgs.Empty);
        }
        private readonly FieldMatrix _matrix;

    }

    /// <summary>Set once a bomb has been revealed.</summary>
    bool _game_lost = false;
    /// <summary>Whether the game is lost: a bomb has been revealed.</summary>
    public bool GameLost
    {
        get => _game_lost;
    }

    /// <summary>
    /// Whether the game is won: not lost, and every field that is not a bomb has been revealed.
    /// </summary>
    public bool GameWon
    {
        get => !_game_lost && _fields.Cast<Field>().All((Field f) => f.IsBomb || !f.Hidden);
    }


    private int _bombs_left;
    private int _fields_left;

    /// <summary>
    /// The number of bombs minus the number of flags placed, like the counter in classic Minesweeper.
    /// Revealed bombs (when the game is lost) don't change it.
    /// </summary>
    public int BombsLeft
    {
        get => _bombs_left;
    }

    /// <summary>
    /// The number of fields that are neither revealed nor flagged.
    /// </summary>
    public int FieldsLeft
    {
        get => _fields_left;
    }
    /// <summary>
    /// Creates an empty board whose bombs are placed by <paramref name="generator"/> on the first call to
    /// <see cref="Initialize(Point{int})"/>.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="bomb_count">The number of bombs to place.</param>
    /// <param name="generator">The generator that places the bombs.</param>
    public FieldMatrix(int rows, int columns, int bomb_count, IMatrixGenerator generator)
    {
        _fields = new Field[rows, columns];
        _rows = rows;
        _columns = columns;

        _bombs_left = bomb_count;
        _fields_left = _rows * _columns;
        _generator = generator;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                _fields[r, c] = new Field(this, r, c, false, new FieldOwnerHandler(this));
            }
        }
    }

    /// <summary>
    /// Creates a new board with the same size and bomb layout as <paramref name="source"/>. All fields
    /// start hidden and unflagged; the board counts as initialized.
    /// </summary>
    /// <param name="source">The board to copy the bombs from.</param>
    public FieldMatrix(FieldMatrix source)
    {
        _initialized = true;
        _rows = source.Rows;
        _columns = source.Columns;
        _fields = new Field[_rows, _columns];
        _fields_left = _rows * _columns;
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(this, r, c, source[r, c].IsBomb, new FieldOwnerHandler(this));
                _bombs_left += source[r, c].IsBomb ? 1 : 0;
            }
        }
    }

    // Board with bombs at chosen positions: bombs[row, column] is true for a bomb.
    /// <summary>
    /// Board with bombs at chosen positions: <c>bombs[row, column]</c> is true for a bomb. The board counts
    /// as initialized.
    /// </summary>
    /// <param name="bombs">The bomb layout, indexed by row and column.</param>
    public FieldMatrix(bool[,] bombs)
    {
        _initialized = true;
        _rows = bombs.GetLength(0);
        _columns = bombs.GetLength(1);
        _fields = new Field[_rows, _columns];
        _fields_left = _rows * _columns;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(this, r, c, bombs[r, c], new FieldOwnerHandler(this));
                _bombs_left += bombs[r, c] ? 1 : 0;
            }
        }
    }

    /// <summary>Backing field for <see cref="Initialized"/>.</summary>
    bool _initialized = false;

    // True once the bombs are placed: right away for a given layout, or on the first reveal with a generator.
    /// <summary>
    /// True once the bombs are placed: right away for a given layout, or on the first reveal with a generator.
    /// </summary>
    public bool Initialized => _initialized;

    /// <summary>
    /// Places the bombs with the generator, keeping <paramref name="first_point"/> in mind. Does nothing if
    /// the board is already initialized.
    /// </summary>
    /// <param name="first_point">The first field the player reveals (X is the row, Y the column).</param>
    public void Initialize(Point<int> first_point)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        var bombs = _generator?.Generate(_rows, _columns, first_point, _bombs_left);
        if (bombs is null)
        {
            return;
        }
        for (int r = 0; r < _rows; ++r)
        {
            for (int c = 0; c < _columns; ++c)
            {
                _fields[r, c].IsBomb = bombs[r, c];
            }
        }
    }

    /// <summary>
    /// Swaps the bomb states of two fields, if exactly one of them is a bomb.
    /// </summary>
    /// <param name="point_a">The first field (X is the row, Y the column).</param>
    /// <param name="point_b">The second field (X is the row, Y the column).</param>
    /// <returns><c>true</c> if the bomb was moved; <c>false</c> if both or neither field is a bomb.</returns>
    public bool SwapBombs(Point<int> point_a, Point<int> point_b)
    {
        if ((!_fields[point_a.X, point_a.Y].IsBomb && !_fields[point_b.X, point_b.Y].IsBomb) ||
           (_fields[point_a.X, point_a.Y].IsBomb && _fields[point_b.X, point_b.Y].IsBomb))
        {
            return false;
        }
        _fields[point_a.X, point_a.Y].IsBomb = !_fields[point_a.X, point_a.Y].IsBomb;
        _fields[point_b.X, point_b.Y].IsBomb = !_fields[point_b.X, point_b.Y].IsBomb;
        return true;
    }

    /// <summary>
    /// Raised when any field on the board is revealed or its flag changes.
    /// </summary>
    public event EventHandler? FieldChanged;

    private Field[,] _fields;

    /// <summary>Places the bombs on <see cref="Initialize(Point{int})"/>; <c>null</c> for boards with a given layout.</summary>
    private IMatrixGenerator? _generator;
    private int _rows;
    private int _columns;

    /// <summary>The number of rows.</summary>
    public int Rows
    {
        get => _rows;
    }

    /// <summary>The number of columns.</summary>
    public int Columns
    {
        get => _columns;
    }

    /// <summary>
    /// The field at the given row and column.
    /// </summary>
    /// <param name="row">The row of the field.</param>
    /// <param name="column">The column of the field.</param>
    public Field this[int row, int column]
    {
        get => _fields[row, column];
    }

    // The field with the given Field.Index (row * Columns + column).
    /// <summary>
    /// The field with the given <see cref="Field.Index"/> (<c>row * Columns + column</c>).
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>The field at that index.</returns>
    public Field FieldAt(int index) => _fields[index / _columns, index % _columns];

    /// <summary>All fields, indexed by row and column.</summary>
    public Field[,] Fields
    {
        get => _fields;
    }
}