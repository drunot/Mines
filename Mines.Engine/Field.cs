using System;
using System.Linq;

namespace Mines;

/// <summary>
/// Event data for <see cref="Field.FieldChanged"/>: tells what changed on a single field.
/// </summary>
public class FieldChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates the event data.
    /// </summary>
    /// <param name="flagged">The new flag state, or <c>null</c> if the flag did not change.</param>
    /// <param name="hidden">The new hidden state, or <c>null</c> if the hidden state did not change.</param>
    /// <param name="is_bomb">Whether the field is a bomb.</param>
    public FieldChangedEventArgs(bool? flagged, bool? hidden, bool is_bomb)
    {
        Flagged = flagged;
        Hidden = hidden;
        IsBomb = is_bomb;
    }

    /// <summary>The new flag state, or <c>null</c> if the flag did not change.</summary>
    public readonly bool? Flagged;
    /// <summary>The new hidden state, or <c>null</c> if the hidden state did not change.</summary>
    public readonly bool? Hidden;
    /// <summary>Whether the field is a bomb.</summary>
    public readonly bool IsBomb;
}

/// <summary>
/// A single cell of a <see cref="FieldMatrix"/>: may hold a bomb, can be hidden/revealed and flagged.
/// </summary>
public class Field
{
    /// <summary>
    /// Creates a hidden, unflagged field at the given row and column of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">The board this field belongs to; its <see cref="FieldMatrix.Columns"/> must already be set.</param>
    /// <param name="own_x">The row of this field.</param>
    /// <param name="own_y">The column of this field.</param>
    /// <param name="is_bomb">Whether this field holds a bomb.</param>
    /// <param name="fieldOwner">Optional owner notified when the field is revealed or its flag changes.</param>
    public Field(FieldMatrix parent, int own_x, int own_y, bool is_bomb = false, IFieldOwner? fieldOwner = null)
    {
        _pos = new Point<int>(own_x, own_y);
        _parent = parent;
        IsBomb = is_bomb;
        _index = own_x * parent.Columns + own_y;
        _fieldOwner = fieldOwner;
    }

    /// <summary>
    /// A single number for this cell (<c>row * Columns + column</c>); the solver uses it instead of
    /// <see cref="Position"/>. See <see cref="FieldMatrix.FieldAt(int)"/> for the reverse lookup.
    /// </summary>
    public int Index => _index;

    private readonly int _index;
    private FieldMatrix _parent;

    /// <summary>The board this field belongs to.</summary>
    public FieldMatrix Parent
    {
        get => _parent;
    }

    /// <summary>
    /// The position of this field on the board: <see cref="Point{T}.X"/> is the row,
    /// <see cref="Point{T}.Y"/> the column.
    /// </summary>
    public Point<int> Position { get => _pos; }

    private Point<int> _pos;


    /// <summary>Whether this field holds a bomb.</summary>
    public bool IsBomb;

    /// <summary>
    /// The number of neighbouring bombs, or <c>null</c> if this field is a bomb itself or has no
    /// neighbouring bombs.
    /// </summary>
    public int? Number
    {
        get
        {
            if (IsBomb) return null;

            int bomb_count = this.Neighbors.Count((Field f) => f.IsBomb);

            return bomb_count == 0 ? null : bomb_count;
        }
    }

    /// <summary>
    /// Whether this field is still hidden (not revealed). Fields start hidden.
    /// </summary>
    /// <remarks>
    /// Setting it raises <see cref="FieldChanged"/> and notifies the owner. Revealing a flagged field
    /// does not reveal it; it only removes the flag.
    /// </remarks>
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
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(null, _hidden, IsBomb));
            _fieldOwner?.OnRevealed(this);
        }
    }

    /// <summary>
    /// Collects the up to eight fields around this one, staying inside the board.
    /// </summary>
    /// <returns>The neighbouring fields, in row-major order.</returns>
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
                if (x_cor == _pos.X && y_cor == _pos.Y)
                {
                    continue;
                }
                result[i++] = _parent[x_cor, y_cor];
            }
        }
        return result;
    }

    private bool _hidden = true;

    /// <summary>
    /// Raised when <see cref="Hidden"/> or <see cref="Flagged"/> changes.
    /// </summary>
    public event EventHandler<FieldChangedEventArgs>? FieldChanged;


    private bool _flagged = false;

    /// <summary>
    /// Whether this field is flagged as a bomb. Only hidden fields can be flagged or unflagged;
    /// setting it on a revealed field does nothing.
    /// </summary>
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
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(_flagged, null, IsBomb));
            _fieldOwner?.OnFlagChanged(value);
        }
    }

    /// <summary>
    /// The fields around this one (up to eight, fewer at the edges). Computed on first use and cached.
    /// </summary>
    public Field[] Neighbors => _neighbors ??= ComputeNeighbors();

    /// <summary>Cache for <see cref="Neighbors"/>; <c>null</c> until first used.</summary>
    private Field[]? _neighbors;

    /// <summary>Notified when this field is revealed or its flag changes; may be <c>null</c>.</summary>
    private IFieldOwner? _fieldOwner;
}