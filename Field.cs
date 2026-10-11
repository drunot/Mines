using System;
using System.Linq;

namespace Mines;




public class FieldChangedEventArgs : EventArgs
{

    public FieldChangedEventArgs(bool? flagged, bool? hidden, bool is_bomb)
    {
        Flagged = flagged;
        Hidden = hidden;
        IsBomb = is_bomb;
    }

    public readonly bool? Flagged;
    public readonly bool? Hidden;
    public readonly bool IsBomb;

}


public class Field
{
    public Field(FieldMatrix parent, int own_x, int own_y, bool is_bomb = false)
    {
        _pos = new Point<int>(own_x, own_y);
        _parent = parent;
        _is_bomb = is_bomb;
        _index = own_x * parent.Columns + own_y;
    }

    // A single number for this cell (row * Columns + column); the solver uses it instead of Position.
    public int Index => _index;

    private readonly int _index;
    private FieldMatrix _parent;

    public FieldMatrix Parent
    {
        get => _parent;
    }

    public Point<int> Position { get => _pos; }

    private Point<int> _pos;


    public bool IsBomb { get => _is_bomb; }

    private bool _is_bomb;

    public int? Number
    {
        get
        {
            if (_is_bomb) return null;

            int bomb_count = this.Neighbors.Count((Field f) => f.IsBomb);

            return bomb_count == 0 ? null : bomb_count;
        }
    }

    public bool Hidden
    {
        get => _hidden; set
        {
            if (value == _hidden)
            {
                return;
            }
            if (!value && _hidden && _flagged)
            {
                Flagged = false;
                return;
            }
            _hidden = value;
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(null, _hidden, _is_bomb));
            if (!_hidden && !IsBomb && Number == null)
            {
                foreach (Field f in this.Neighbors)
                {
                    f.Hidden = false;
                }
                return;
            }
            if (IsBomb)
            {
                foreach (Field grid_field in _parent.Fields)
                {
                    if (grid_field.IsBomb && !grid_field.Flagged)
                    {
                        grid_field.Hidden = false;
                    }
                }
            }
        }
    }

    private Field[] ComputeNeighbors()
    {
        var x_min = Math.Max(_pos.X - 1, 0);
        var x_max = Math.Min(_pos.X + 1, _parent.Rows - 1);
        var y_min = Math.Max(_pos.Y - 1, 0);
        var y_max = Math.Min(_pos.Y + 1, _parent.Columns - 1);
        var result = new Field[(x_max - x_min + 1) * (y_max - y_min + 1) - 1];
        int i = 0;
        for (int x_cor = x_min; x_cor <= x_max; ++x_cor)
        {
            for (int y_cor = y_min; y_cor <= y_max; ++y_cor)
            {
                if(x_cor == _pos.X && y_cor == _pos.Y)
                {
                    continue;
                }
                result[i++] = _parent[x_cor, y_cor];
            }
        }
        return result;
    }

    private bool _hidden = true;

    public event EventHandler<FieldChangedEventArgs>? FieldChanged;


    private bool _flagged = false;

    public bool Flagged
    {
        get => _flagged;
        set
        {
            if (!_hidden || _flagged == value)
            {
                return;
            }
            _flagged = value;
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(_flagged, null, _is_bomb));
        }
    }

    public Field[] Neighbors => _neighbors ??= ComputeNeighbors();

    private Field[]? _neighbors;
}