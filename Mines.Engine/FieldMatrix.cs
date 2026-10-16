using System;
using System.Linq;
using Mines.Generators;

namespace Mines;

/// <summary>
/// The Minesweeper board: a grid of <see cref="Field"/>s indexed by row and column, plus the game
/// state (bombs left, won/lost).
/// </summary>
public class FieldMatrix : IBoard
{
    class FieldOwner : IFieldOwner
    {
        public FieldOwner(FieldMatrix fieldMatrix)
        {
            _matrix = fieldMatrix;
        }

        public Field? FieldAt(long row, long column)
        {
            if (row >= _matrix._rows || row < 0 || column >= _matrix.Columns || column < 0)
            {
                return null;
            }
            return _matrix._fields[row, column];
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
        public void OnFlagChanged(Field field)
        {

            if (field.Flagged == true)
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
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, false);
            }
        }
    }

    // WORK IN PROGRESS CONSTRICTOR FOR INFINITE FIELD
    /// <summary>
    /// Creates one chunk of an infinite board: a square of <paramref name="chunk_size"/> by
    /// <paramref name="chunk_size"/> hidden fields without bombs. The infinite board places them with
    /// <see cref="ApplyBombs"/>.
    /// </summary>
    /// <remarks>
    /// The fields get global positions: chunk (<paramref name="chunk_idx_r"/>, <paramref name="chunk_idx_c"/>)
    /// starts at row <c>chunk_size * chunk_idx_r</c> and column <c>chunk_size * chunk_idx_c</c>, so chunk
    /// (-1, 0) starts at row -<paramref name="chunk_size"/>. Their <see cref="Field.Index"/> stays local to
    /// the chunk.
    /// </remarks>
    /// <param name="chunk_size">The number of rows and columns of the chunk.</param>
    /// <param name="chunk_idx_r">The chunk's row among the chunks; may be negative.</param>
    /// <param name="chunk_idx_c">The chunk's column among the chunks; may be negative.</param>
    /// <param name="chunk_bomb_count">The number of bombs the chunk will get, for <see cref="BombsLeft"/>.</param>
    /// <param name="owner">
    /// The infinite board's owner of all fields: finds fields by global position across all chunks, and handles
    /// reveals (the cascade) and flags.
    /// </param>
    public FieldMatrix(int chunk_size, long chunk_idx_r, long chunk_idx_c, int chunk_bomb_count, IFieldOwner owner)
    {
        _fields = new Field[chunk_size, chunk_size];
        _rows = chunk_size;
        _columns = chunk_size;
        _chunk_idx_r = chunk_idx_r;
        _chunk_idx_c = chunk_idx_c;
        _fields_left = _rows * _columns;
        _bombs_left = chunk_bomb_count;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(owner, chunk_size * chunk_idx_r + r, chunk_size * chunk_idx_c + c, r * _columns + c, false);
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
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, source[r, c].IsBomb);
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
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, bombs[r, c]);
                _bombs_left += bombs[r, c] ? 1 : 0;
            }
        }
    }


    /// <summary>
    /// Board with bombs at chosen positions and some cells already revealed, e.g. a copy of part of an infinite
    /// board to solve. The board counts as initialized; setting the hidden state doesn't cascade.
    /// </summary>
    /// <param name="bombs">The bomb layout, indexed by row and column.</param>
    /// <param name="hidden">Which cells are hidden, indexed by row and column.</param>
    public FieldMatrix(bool[,] bombs, bool[,] hidden)
    {
        _initialized = true;
        _rows = bombs.GetLength(0);
        _columns = bombs.GetLength(1);
        _fields = new Field[_rows, _columns];
        _fields_left = _rows * _columns;
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, bombs[r, c]);
                _fields[r, c].SetHiddenNoPropagate(hidden[r, c]);
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
        _generator?.Generate(this, first_point, _bombs_left);
    }

    /// <summary>
    /// Sets the bombs of every field from a layout. Used by the generators.
    /// </summary>
    /// <param name="bombs">The layout, indexed by the local row and column: <c>[row, column]</c> is true for a bomb.</param>
    /// <exception cref="ArgumentException"><paramref name="bombs"/> doesn't have the board's size.</exception>
    public void ApplyBombs(bool[,] bombs)
    {
        if (bombs.GetLength(0) != _rows || bombs.GetLength(1) != _columns)
        {
            throw new ArgumentException("ApplyBombs called with wrong arguments.");
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
    public bool SwapBombs(Point<long> point_a, Point<long> point_b)
    {
        long x_mod = _chunk_idx_r * _rows;
        long y_mod = _chunk_idx_c * _columns;
        int a_x = (int)(point_a.X - x_mod);
        int a_y = (int)(point_a.Y - y_mod);
        int b_x = (int)(point_b.X - x_mod);
        int b_y = (int)(point_b.Y - y_mod);

        if ((!_fields[a_x, a_y].IsBomb && !_fields[b_x, b_y].IsBomb) ||
           (_fields[a_x, a_y].IsBomb && _fields[b_x, b_y].IsBomb))
        {
            return false;
        }
        _fields[a_x, a_y].IsBomb = !_fields[a_x, a_y].IsBomb;
        _fields[b_x, b_y].IsBomb = !_fields[b_x, b_y].IsBomb;
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

    private long _chunk_idx_r = 0;
    private long _chunk_idx_c = 0;

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


    /// <summary>
    /// Map each field to a bool.
    /// </summary>
    /// <returns>The fields.</returns>
    private bool[,] Map(Func<Field, bool> test)
    {
        var result = new bool[Rows, Columns];
        for (int r = 0; r < Rows; ++r)
        {
            for (int c = 0; c < Columns; ++c)
            {
                result[r, c] = test(_fields[r, c]);
            }
        }
        return result;
    }

    /// <summary>
    /// Get the fields which are bombs.
    /// </summary>
    /// <returns>The fields.</returns>
    public bool[,] GetBombs() => Map(f => f.IsBomb);

    /// <summary>
    /// Get the fields which are hidden.
    /// </summary>
    /// <returns>The fields.</returns>
    public bool[,] GetHidden() => Map(f => f.Hidden);

    /// <summary>
    /// Get the fields which will change fields visible for the user and should be locked for a board fixer.
    /// </summary>
    /// <returns>The fields.</returns>
    public bool[,] GetLocked() => Map(f => !f.Hidden || f.Neighbors.Any(n => !n.Hidden));

    // ---- IBoard: how the view plays a finite board ----

    /// <inheritdoc/>
    public BoardSize? Size => new BoardSize(_rows, _columns);

    /// <inheritdoc/>
    public Field? FieldAt(long row, long column)
    {
        if (row < 0 || row >= _rows || column < 0 || column >= _columns)
        {
            return null;
        }
        return _fields[row, column];
    }

    /// <inheritdoc/>
    public void Reveal(long row, long column)
    {
        if (FieldAt(row, column) is not Field field)
        {
            return;
        }
        Initialize(new Point<int>((int)row, (int)column));   // the first click generates the board
        if (!field.Hidden)
        {
            // Chord: a revealed number with all its bombs flagged reveals the other neighbours.
            if (field.Number is int number && number <= field.Neighbors.Count(f => f.Flagged))
            {
                foreach (Field neighbor in field.Neighbors)
                {
                    if (!neighbor.Flagged)
                    {
                        neighbor.Hidden = false;
                    }
                }
            }
            return;
        }
        field.Hidden = false;
    }

    /// <inheritdoc/>
    public void CycleMark(long row, long column)
    {
        if (FieldAt(row, column) is not Field field || !field.Hidden)
        {
            return;
        }
        CycleMark(field);
        FieldChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves a hidden field to its next mark: none, flag, question mark, none. Shared with
    /// <see cref="InfiniteFieldMatrix"/>.
    /// </summary>
    /// <param name="field">A hidden field.</param>
    internal static void CycleMark(Field field)
    {
        field.Mark = field.Mark switch
        {
            Mark.None => Mark.Flag,
            Mark.Flag => Mark.Question,
            _ => Mark.None,
        };
    }

    /// <inheritdoc/>
    /// <remarks>A finite board has no hidden work to do for the visible area, so this does nothing.</remarks>
    public void SetVisibleArea(long top, long left, long bottom, long right)
    {
    }

    // The field with the given Field.Index (row * Columns + column).
    /// <summary>
    /// The field with the given <see cref="Field.Index"/> (<c>row * Columns + column</c>).
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>The field at that index.</returns>
    public Field FieldAt(int index) => _fields[index / _columns, index % _columns];

    /// <summary>
    /// The position of <paramref name="field"/> inside this board, for indexing <see cref="this[int, int]"/>
    /// and layouts like <see cref="GetBombs"/>. On a normal board it equals <see cref="Field.Position"/>; on a
    /// chunk of an infinite board it doesn't.
    /// </summary>
    /// <param name="field">A field of this board.</param>
    /// <returns>The local position: X is the row, Y the column.</returns>
    public Point<int> LocalPosition(Field field) => new Point<int>(field.Index / _columns, field.Index % _columns);

    /// <summary>All fields, indexed by row and column.</summary>
    public Field[,] Fields
    {
        get => _fields;
    }
}