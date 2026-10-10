using System;

namespace Mines;

public class FieldMatrix
{

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
    public FieldMatrix(int rows, int columns, int bomb_count)
    {
        _fields = new Field[rows, columns];
        _rows = rows;
        _columns = columns;

        _bombs_left = bomb_count;
        _fields_left = _rows * _columns;


        var rand = new Random();
        Point<int>[] bombs = new Point<int>[bomb_count];

        for (int i = 0; i < bomb_count; ++i)
        {
            var random_number = new Point<int>(rand.Next() % rows, rand.Next() % columns);
            while (bombs.Contains(random_number))
            {
                random_number = new Point<int>(rand.Next() % rows, rand.Next() % columns);
            }
            bombs[i] = random_number;
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                bool is_bomb = bombs.Contains(new Point<int>(r, c));
                _fields[r, c] = new Field(this, r, c, is_bomb);

                _fields[r, c].FieldChanged += FieldChangedHandler;
            }
        }
    }

    public FieldMatrix(FieldMatrix source)
    {
        _rows = source.Rows;
        _columns = source.Columns;
        _fields = new Field[_rows, _columns];
        _fields_left = _rows * _columns;
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(this, r, c, source[r, c].IsBomb);
                _fields[r, c].FieldChanged += FieldChangedHandler;
                _bombs_left += source[r, c].IsBomb ? 1 : 0;
            }
        }
    }

    // Board with bombs at chosen positions: bombs[row, column] is true for a bomb.
    public FieldMatrix(bool[,] bombs)
    {
        _rows = bombs.GetLength(0);
        _columns = bombs.GetLength(1);
        _fields = new Field[_rows, _columns];
        _fields_left = _rows * _columns;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _columns; c++)
            {
                _fields[r, c] = new Field(this, r, c, bombs[r, c]);
                _fields[r, c].FieldChanged += FieldChangedHandler;
                _bombs_left += bombs[r, c] ? 1 : 0;
            }
        }
    }

    private void FieldChangedHandler(Object? o, FieldChangedEventArgs e)
    {
        if (e.Flagged == true)
        {
            _fields_left -= 1;
            _bombs_left -= 1;
        }

        if (e.Flagged == false)
        {
            _fields_left += 1;
            _bombs_left += 1;
        }

        if (e.Hidden == true)
        {
            // Stop handling events.
            _fields_left += 1;
            if (e.IsBomb)
            {
                _bombs_left += 1;
            }

        }
        if (e.Hidden == false)
        {
            // Stop handling events.
            _fields_left -= 1;
            if (e.IsBomb)
            {
                _bombs_left -= 1;
            }
        }
        FieldChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? FieldChanged;

    private Field[,] _fields;


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

    public Field[,] Fields
    {
        get => _fields;
    }
}