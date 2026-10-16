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
    /// Creates a hidden, unflagged field at the given global row and column.
    /// </summary>
    /// <param name="fieldOwner">
    /// The board the field belongs to: finds fields by global position (for this field's
    /// <see cref="Neighbors"/>, also across chunk borders on an infinite board) and is told when the field is
    /// revealed or its flag changes.
    /// </param>
    /// <param name="own_x">The global row of this field (on a normal board, the same as its row on the board).</param>
    /// <param name="own_y">The global column of this field (on a normal board, the same as its column on the board).</param>
    /// <param name="index">
    /// The field's <see cref="Index"/>: <c>row * Columns + column</c> with the row and column inside its own
    /// <see cref="FieldMatrix"/>, not the global ones.
    /// </param>
    /// <param name="is_bomb">Whether this field holds a bomb.</param>
    public Field(IFieldOwner fieldOwner, long own_x, long own_y, int index, bool is_bomb = false)
    {
        _pos = new Point<long>(own_x, own_y);
        IsBomb = is_bomb;
        _index = index;
        _fieldOwner = fieldOwner;
    }

    /// <summary>
    /// A single number for this cell (<c>row * Columns + column</c>); the solver uses it instead of
    /// <see cref="Position"/>. See <see cref="FieldMatrix.FieldAt(int)"/> for the reverse lookup.
    /// </summary>
    public int Index => _index;

    private readonly int _index;

    /// <summary>
    /// The global position of this field: <see cref="Point{T}.X"/> is the row, <see cref="Point{T}.Y"/> the
    /// column. On a normal board it is the same as the position on the board; on an infinite board it may be
    /// negative. Use <see cref="FieldMatrix.LocalPosition"/> to index the field's own <see cref="FieldMatrix"/>.
    /// </summary>
    public Point<long> Position { get => _pos; }

    private Point<long> _pos;


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
    /// does not reveal it; it only removes the flag. Revealing a question-marked field removes the question mark.
    /// </remarks>
    public bool Hidden
    {
        get => _hidden; set
        {
            if (value == _hidden)
            {
                return;
            }
            if (!value && _hidden && Flagged)
            {
                Flagged = false;
                return;
            }
            if (!value)
            {
                _mark = Mark.None;   // a revealed field has no mark (only a question mark can be left here)
            }
            _hidden = value;
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(null, _hidden, IsBomb));
            _fieldOwner?.OnRevealed(this);
        }
    }

    /// <summary>
    /// Sets <see cref="Hidden"/> without telling anyone: no event, no owner notification, no cascade. For building
    /// a copy of a board in a given state, such as the window an infinite board is solved in.
    /// </summary>
    /// <param name="hidden">Whether the field is hidden.</param>
    public void SetHiddenNoPropagate(bool hidden)
    {
        _hidden = hidden;
    }

    /// <summary>
    /// Collects the up to eight fields around this one, staying inside the board.
    /// </summary>
    /// <returns>The neighbouring fields, in row-major order.</returns>
    private Field[] ComputeNeighbors()
    {
        var result = new Field[8];
        int i = 0;
        for (long x_cor = _pos.X - 1; x_cor <= _pos.X + 1; ++x_cor)
        {
            for (long y_cor = _pos.Y - 1; y_cor <= _pos.Y + 1; ++y_cor)
            {
                if (x_cor == _pos.X && y_cor == _pos.Y)
                {
                    continue;
                }
                var field = _fieldOwner.FieldAt(x_cor, y_cor);
                if (field is not null)
                {

                    result[i++] = field;
                }
            }
        }

        return result[..i];   
    }

    private bool _hidden = true;

    /// <summary>
    /// Raised when <see cref="Hidden"/> or <see cref="Mark"/> changes.
    /// </summary>
    public event EventHandler<FieldChangedEventArgs>? FieldChanged;


    /// <summary>Backing field for <see cref="Mark"/>.</summary>
    private Mark _mark = Mark.None;

    /// <summary>
    /// The player's mark on this field: none, a flag or a question mark. Only a hidden field can be marked;
    /// setting it on a revealed field does nothing, and revealing a field removes its mark.
    /// </summary>
    /// <remarks>
    /// Raises <see cref="FieldChanged"/>. The owner is told (<see cref="IFieldOwner.OnFlagChanged"/>) only when
    /// the field becomes flagged or stops being flagged, since only flags count for anything.
    /// </remarks>
    public Mark Mark
    {
        get => _mark;
        set
        {
            if (!_hidden || _mark == value)
            {
                return;
            }
            bool was_flagged = _mark == Mark.Flag;
            _mark = value;
            FieldChanged?.Invoke(this, new FieldChangedEventArgs(Flagged, null, IsBomb));
            if (was_flagged != Flagged)
            {
                _fieldOwner.OnFlagChanged(this);
            }
        }
    }

    /// <summary>
    /// Whether this field is flagged as a bomb: a shortcut for <see cref="Mark"/> being <see cref="Mark.Flag"/>.
    /// Setting it to <c>false</c> only removes a flag, not a question mark.
    /// </summary>
    public bool Flagged
    {
        get => _mark == Mark.Flag;
        set
        {
            if (value)
            {
                Mark = Mark.Flag;
            }
            else if (_mark == Mark.Flag)
            {
                Mark = Mark.None;
            }
        }
    }

    /// <summary>
    /// Whether this field has a question mark: a shortcut for <see cref="Mark"/> being <see cref="Mark.Question"/>.
    /// Setting it to <c>false</c> only removes a question mark, not a flag.
    /// </summary>
    public bool Questioned
    {
        get => _mark == Mark.Question;
        set
        {
            if (value)
            {
                Mark = Mark.Question;
            }
            else if (_mark == Mark.Question)
            {
                Mark = Mark.None;
            }
        }
    }

    /// <summary>
    /// The fields around this one (up to eight, fewer at the edges). Computed on first use and cached.
    /// </summary>
    public Field[] Neighbors => _neighbors ??= ComputeNeighbors();

    /// <summary>Cache for <see cref="Neighbors"/>; <c>null</c> until first used.</summary>
    private Field[]? _neighbors;

    /// <summary>Notified when this field is revealed or its flag changes; may be <c>null</c>.</summary>
    private IFieldOwner _fieldOwner;
}