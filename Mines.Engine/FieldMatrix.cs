using System;
using System.Linq;
using Mines.Generators;

namespace Mines;

/// <summary>
/// A finite Minesweeper board: a grid of <see cref="Field"/>s indexed by row and column, plus the game
/// state (bombs left, won/lost).
/// </summary>
/// <remarks>
/// Also used as one chunk of an <see cref="InfiniteFieldMatrix"/>, and as the window copy of 3 by 3 chunks
/// that an infinite board is solved in.
/// </remarks>
public class FieldMatrix : IBoard
{
    /// <summary>
    /// The owner of the fields of a finite board: finds neighbours inside the board, runs the cascade, keeps
    /// the counters and ends the game when a bomb is revealed. Chunks of an infinite board use the infinite
    /// board's owner instead.
    /// </summary>
    class FieldOwner : IFieldOwner
    {
        /// <summary>
        /// Creates the owner for <paramref name="fieldMatrix"/>.
        /// </summary>
        /// <param name="fieldMatrix">The board the fields belong to.</param>
        public FieldOwner(FieldMatrix fieldMatrix)
        {
            _matrix = fieldMatrix;
        }

        /// <inheritdoc/>
        public Field? FieldAt(long row, long column)
        {
            if (row >= _matrix._rows || row < 0 || column >= _matrix.Columns || column < 0)
            {
                return null;
            }
            return _matrix._fields[row, column];
        }

        /// <inheritdoc/>
        /// <remarks>
        /// A revealed cell without neighbouring bombs reveals its neighbours; a revealed bomb reveals every
        /// unflagged bomb and loses the game.
        /// </remarks>
        public void OnRevealed(Field field)
        {
            _matrix._fieldsLeft += field.Hidden ? 1 : -1;   // the field can also be hidden again
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
                foreach (Field gridField in _matrix.Fields)
                {
                    if (gridField.IsBomb && !gridField.Flagged)
                    {
                        gridField.Hidden = false;
                    }
                }
                _matrix._gameLost = true;
            }
            _matrix.FieldChanged?.Invoke(_matrix, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void OnFlagChanged(Field field)
        {

            if (field.Flagged == true)
            {
                _matrix._fieldsLeft -= 1;
                _matrix._bombsLeft -= 1;
            }
            else
            {
                _matrix._fieldsLeft += 1;
                _matrix._bombsLeft += 1;
            }
            _matrix.FieldChanged?.Invoke(_matrix, EventArgs.Empty);
        }

        /// <summary>The board the fields belong to.</summary>
        private readonly FieldMatrix _matrix;

    }

    /// <summary>Set once a bomb has been revealed.</summary>
    bool _gameLost = false;
    /// <summary>Whether the game is lost: a bomb has been revealed.</summary>
    public bool GameLost
    {
        get => _gameLost;
    }

    /// <summary>
    /// Whether the game is won: not lost, and every field that is not a bomb has been revealed.
    /// </summary>
    public bool GameWon
    {
        get => !_gameLost && _fields.Cast<Field>().All(f => f.IsBomb || !f.Hidden);
    }

    /// <summary>Backing field for <see cref="BombsLeft"/>.</summary>
    private int _bombsLeft;
    /// <summary>Backing field for <see cref="FieldsLeft"/>.</summary>
    private int _fieldsLeft;

    /// <summary>
    /// The number of bombs minus the number of flags placed, like the counter in classic Minesweeper.
    /// Revealed bombs (when the game is lost) don't change it.
    /// </summary>
    public int BombsLeft
    {
        get => _bombsLeft;
    }

    /// <summary>
    /// The number of fields that are neither revealed nor flagged.
    /// </summary>
    public int FieldsLeft
    {
        get => _fieldsLeft;
    }

    /// <summary>
    /// Creates an empty board whose bombs are placed by <paramref name="generator"/> on the first call to
    /// <see cref="Initialize(Point{int})"/>.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="bombCount">The number of bombs to place.</param>
    /// <param name="generator">The generator that places the bombs.</param>
    public FieldMatrix(int rows, int columns, int bombCount, IMatrixGenerator generator)
    {
        _fields = new Field[rows, columns];
        _rows = rows;
        _columns = columns;

        _bombsLeft = bombCount;
        _fieldsLeft = _rows * _columns;
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

    /// <summary>
    /// Creates one chunk of an infinite board: a square of <paramref name="chunkSize"/> by
    /// <paramref name="chunkSize"/> hidden fields without bombs. The infinite board places them with
    /// <see cref="ApplyBombs"/>.
    /// </summary>
    /// <remarks>
    /// The fields get global positions: chunk (<paramref name="chunkIdxR"/>, <paramref name="chunkIdxC"/>)
    /// starts at row <c>chunkSize * chunkIdxR</c> and column <c>chunkSize * chunkIdxC</c>, so chunk
    /// (-1, 0) starts at row -<paramref name="chunkSize"/>. Their <see cref="Field.Index"/> stays local to
    /// the chunk.
    /// </remarks>
    /// <param name="chunkSize">The number of rows and columns of the chunk.</param>
    /// <param name="chunkIdxR">The chunk's row among the chunks; may be negative.</param>
    /// <param name="chunkIdxC">The chunk's column among the chunks; may be negative.</param>
    /// <param name="chunkBombCount">The number of bombs the chunk will get, for <see cref="BombsLeft"/>.</param>
    /// <param name="owner">
    /// The infinite board's owner of all fields: finds fields by global position across all chunks, and handles
    /// reveals (the cascade) and flags.
    /// </param>
    public FieldMatrix(int chunkSize, long chunkIdxR, long chunkIdxC, int chunkBombCount, IFieldOwner owner)
    {
        _fields = new Field[chunkSize, chunkSize];
        _rows = chunkSize;
        _columns = chunkSize;
        _chunkIdxR = chunkIdxR;
        _chunkIdxC = chunkIdxC;
        _fieldsLeft = _rows * _columns;
        _bombsLeft = chunkBombCount;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(owner, chunkSize * chunkIdxR + r, chunkSize * chunkIdxC + c,
                                          r * _columns + c, false);
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
        _fieldsLeft = _rows * _columns;
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, source[r, c].IsBomb);
                _bombsLeft += source[r, c].IsBomb ? 1 : 0;
            }
        }
    }

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
        _fieldsLeft = _rows * _columns;
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, bombs[r, c]);
                _bombsLeft += bombs[r, c] ? 1 : 0;
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
        _fieldsLeft = _rows * _columns;
        var fieldOwner = new FieldOwner(this);

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(fieldOwner, r, c, r * _columns + c, bombs[r, c]);
                _fields[r, c].SetHiddenNoPropagate(hidden[r, c]);
                _bombsLeft += bombs[r, c] ? 1 : 0;
            }
        }
    }

    /// <summary>Backing field for <see cref="Initialized"/>.</summary>
    bool _initialized = false;

    /// <summary>
    /// True once the bombs are placed: right away for a given layout, or on the first reveal with a generator.
    /// </summary>
    public bool Initialized => _initialized;

    /// <summary>
    /// Places the bombs with the generator (if the board has one), keeping <paramref name="firstPoint"/> in mind.
    /// Does nothing if the board is already initialized.
    /// </summary>
    /// <param name="firstPoint">The first field the player reveals (X is the row, Y the column).</param>
    public void Initialize(Point<int> firstPoint)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        _generator?.Generate(this, firstPoint, _bombsLeft);
    }

    /// <summary>
    /// Sets the bombs of every field from a layout. Used by the generators and by the infinite board for its
    /// chunks. Doesn't change <see cref="BombsLeft"/>.
    /// </summary>
    /// <param name="bombs">
    /// The layout, indexed by the local row and column: <c>[row, column]</c> is true for a bomb.
    /// </param>
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
    /// <param name="pointA">The global position of the first field (<see cref="Field.Position"/>).</param>
    /// <param name="pointB">The global position of the second field (<see cref="Field.Position"/>).</param>
    /// <returns><c>true</c> if the bomb was moved; <c>false</c> if both or neither field is a bomb.</returns>
    public bool SwapBombs(Point<long> pointA, Point<long> pointB)
    {
        long xMod = _chunkIdxR * _rows;
        long yMod = _chunkIdxC * _columns;
        int aX = (int)(pointA.X - xMod);
        int aY = (int)(pointA.Y - yMod);
        int bX = (int)(pointB.X - xMod);
        int bY = (int)(pointB.Y - yMod);

        if ((!_fields[aX, aY].IsBomb && !_fields[bX, bY].IsBomb) ||
           (_fields[aX, aY].IsBomb && _fields[bX, bY].IsBomb))
        {
            return false;
        }
        _fields[aX, aY].IsBomb = !_fields[aX, aY].IsBomb;
        _fields[bX, bY].IsBomb = !_fields[bX, bY].IsBomb;
        return true;
    }

    /// <summary>
    /// Raised when a field of the board is revealed, hidden again or its flag changes, and after
    /// <see cref="CycleMark(long, long)"/>. Not raised for a chunk of an infinite board, whose fields tell the
    /// infinite board instead.
    /// </summary>
    public event EventHandler? FieldChanged;

    /// <summary>The fields, indexed by the local row and column.</summary>
    private Field[,] _fields;

    /// <summary>
    /// Places the bombs on <see cref="Initialize(Point{int})"/>; <c>null</c> for boards with a given layout and
    /// for chunks.
    /// </summary>
    private IMatrixGenerator? _generator;
    /// <summary>Backing field for <see cref="Rows"/>.</summary>
    private int _rows;
    /// <summary>Backing field for <see cref="Columns"/>.</summary>
    private int _columns;

    /// <summary>
    /// The chunk's row among the chunks of an infinite board; 0 otherwise. Turns global positions into local ones
    /// in <see cref="SwapBombs"/>.
    /// </summary>
    private long _chunkIdxR = 0;
    /// <summary>The chunk's column among the chunks of an infinite board; 0 otherwise.</summary>
    private long _chunkIdxC = 0;

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
    /// The field at the given local row and column (the same as the global ones, except on a chunk).
    /// </summary>
    /// <param name="row">The local row of the field.</param>
    /// <param name="column">The local column of the field.</param>
    public Field this[int row, int column]
    {
        get => _fields[row, column];
    }

    /// <summary>
    /// Maps each field to a bool.
    /// </summary>
    /// <param name="test">The function to apply to each field.</param>
    /// <returns><c>[row, column]</c> is the result of <paramref name="test"/> for that field.</returns>
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
    /// Gets the bomb layout.
    /// </summary>
    /// <returns><c>[row, column]</c> is true for a bomb.</returns>
    public bool[,] GetBombs() => Map(f => f.IsBomb);

    /// <summary>
    /// Gets which fields are hidden.
    /// </summary>
    /// <returns><c>[row, column]</c> is true for a hidden field.</returns>
    public bool[,] GetHidden() => Map(f => f.Hidden);

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
