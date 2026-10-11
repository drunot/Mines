using System;
using System.Linq;
using Mines.Generators;

namespace Mines;


public interface IFieldOwner
{
    void OnRevealed(Field field);
    void OnFlagChanged(bool flagged);
}
public class FieldMatrix
{

    class FieldOwnerHandler : IFieldOwner
    {

        public FieldOwnerHandler(FieldMatrix matrix)
        {
            _matrix = matrix;
        }

        public void OnRevealed(Field field)
        {
            _matrix._fields_left -= 1;
            if (field.IsBomb)
            {
                _matrix._bombs_left -= 1;
            }

            if (!field.Hidden && !field.IsBomb && field.Number == null)
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

    bool _game_lost = false;
    public bool GameLost
    {
        get => _game_lost;
    }

    public bool GameWon
    {
        get => !_game_lost && _fields.Cast<Field>().All((Field f) => f.IsBomb || !f.Hidden);
    }

    // The part of FieldMatrix its fields may use. FieldMatrix gives it only to its own fields.

    private int _bombs_left;
    private int _fields_left;

    public int BombsLeft
    {
        get => _bombs_left;
    }

    public int FieldsLeft
    {
        get => _fields_left;
    }
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

    bool _initialized = false;

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

    public event EventHandler? FieldChanged;

    private Field[,] _fields;

    private IMatrixGenerator? _generator;
    private int _rows;
    private int _columns;

    public int Rows
    {
        get => _rows;
    }

    public int Columns
    {
        get => _columns;
    }

    public Field this[int row, int column]
    {
        get => _fields[row, column];
    }

    // The field with the given Field.Index (row * Columns + column).
    public Field FieldAt(int index) => _fields[index / _columns, index % _columns];

    public Field[,] Fields
    {
        get => _fields;
    }
}