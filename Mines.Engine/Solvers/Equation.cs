using System.Collections.Generic;
using System;

namespace Mines.Solving;

/// <summary>
/// "The bombs among these cells add up to sum." Cells are <see cref="Field.Index"/> values, kept sorted
/// in a small array: equations rarely have more than 8 cells, so scanning beats hashing.
/// </summary>
/// <remarks>
/// Value equality (<see cref="Equals(Equation)"/>) compares the sum and the cells; use
/// <see cref="EquationIdentity"/> to compare by identity.
/// </remarks>
public class Equation : IEquatable<Equation>
{
    /// <summary>
    /// Creates an empty equation with sum 0.
    /// </summary>
    /// <param name="capacity">The number of cells to reserve room for.</param>
    public Equation(int capacity = 8)
    {
        sum = 0;
        _cells = new int[capacity];
    }

    /// <summary>
    /// Creates a copy of <paramref name="equation"/> with the same cells and sum (but a new <see cref="Id"/>).
    /// </summary>
    /// <param name="equation">The equation to copy.</param>
    public Equation(Equation equation)
    {
        sum = equation.sum;
        _cells = equation.Cells.ToArray();
        _count = equation._count;
    }

    /// <summary>The number of bombs among <see cref="Cells"/>.</summary>
    public int sum;

    /// <summary>The number of cells in the equation.</summary>
    public int Count => _count;

    /// <summary>
    /// The cells in ascending order. Don't keep it while changing this equation.
    /// </summary>
    public ReadOnlySpan<int> Cells => _cells.AsSpan(0, _count);

    /// <summary>The smallest cell. Only valid if the equation is not empty.</summary>
    public int First => _cells[0];

    /// <summary>
    /// A copy of the cells, for looping while the equation changes.
    /// </summary>
    /// <returns>The cells in ascending order.</returns>
    public int[] ToArray() => Cells.ToArray();

    /// <summary>
    /// Adds a cell, keeping the cells sorted. Does nothing if the cell is already in the equation.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    public void Add(int cell)
    {
        int position = Array.BinarySearch(_cells, 0, _count, cell);
        if (position >= 0)
        {
            return;   // already in the equation
        }
        position = ~position;
        if (_count == _cells.Length)
        {
            Array.Resize(ref _cells, Math.Max(4, _cells.Length * 2));
        }
        Array.Copy(_cells, position, _cells, position + 1, _count - position);
        _cells[position] = cell;
        _count++;
    }

    /// <summary>
    /// Whether the equation contains <paramref name="cell"/>.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    /// <returns><c>true</c> if the cell is in the equation.</returns>
    public bool Contains(int cell) => Array.BinarySearch(_cells, 0, _count, cell) >= 0;

    /// <summary>
    /// Removes a cell. The sum is not changed.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    /// <returns><c>true</c> if the cell was in the equation.</returns>
    public bool Remove(int cell)
    {
        int position = Array.BinarySearch(_cells, 0, _count, cell);
        if (position < 0)
        {
            return false;
        }
        Array.Copy(_cells, position + 1, _cells, position, _count - position - 1);
        _count--;
        return true;
    }

    /// <summary>
    /// Is every cell of this equation also in <paramref name="other"/>? Walks both sorted arrays side by side.
    /// </summary>
    /// <param name="other">The equation to compare with.</param>
    /// <returns><c>true</c> if this equation's cells are a subset of <paramref name="other"/>'s.</returns>
    public bool IsSubsetOf(Equation other)
    {
        if (_count > other._count)
        {
            return false;
        }
        int j = 0;
        for (int i = 0; i < _count; i++)
        {
            while (j < other._count && other._cells[j] < _cells[i])
            {
                j++;
            }
            if (j == other._count || other._cells[j] != _cells[i])
            {
                return false;
            }
            j++;
        }
        return true;
    }

    /// <summary>
    /// This equation without the cells of <paramref name="smaller"/> (expected to be a subset of it).
    /// </summary>
    /// <param name="smaller">The equation to subtract.</param>
    /// <returns>A new equation with the remaining cells and the difference of the sums.</returns>
    public Equation Minus(Equation smaller)
    {
        var difference = new Equation(Math.Max(1, _count - smaller._count));
        int j = 0;
        for (int i = 0; i < _count; i++)
        {
            while (j < smaller._count && smaller._cells[j] < _cells[i])
            {
                j++;
            }
            if (j < smaller._count && smaller._cells[j] == _cells[i])
            {
                continue;
            }
            difference._cells[difference._count++] = _cells[i];   // stays sorted
        }
        difference.sum = sum - smaller.sum;
        return difference;
    }

    /// <summary>
    /// Value equality: same sum and same cells.
    /// </summary>
    /// <param name="other">The equation to compare with.</param>
    /// <returns><c>true</c> if both equations have the same sum and cells.</returns>
    public bool Equals(Equation? other) => other is not null && sum == other.sum && Cells.SequenceEqual(other.Cells);
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is Equation equation && Equals(equation);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(sum);
        foreach (int cell in Cells)
        {
            hash.Add(cell);
        }
        return hash.ToHashCode();
    }

    /// <summary>Value equality, see <see cref="Equals(Equation)"/>.</summary>
    /// <param name="left">The first equation; must not be <c>null</c>.</param>
    /// <param name="right">The second equation.</param>
    /// <returns><c>true</c> if both equations have the same sum and cells.</returns>
    public static bool operator ==(Equation left, Equation right) => left.Equals(right);
    /// <summary>Value inequality, see <see cref="Equals(Equation)"/>.</summary>
    /// <param name="left">The first equation; must not be <c>null</c>.</param>
    /// <param name="right">The second equation.</param>
    /// <returns><c>true</c> if the equations differ in sum or cells.</returns>
    public static bool operator !=(Equation left, Equation right) => !left.Equals(right);

    /// <summary>
    /// Bit r is set while this equation waits in <see cref="EquationList"/>'s queue for rule r
    /// (see <see cref="Rule"/>).
    /// </summary>
    internal byte QueuedRules;

    /// <summary>
    /// Unique per equation; a cheap identity hash for <see cref="EquationIdentity"/>.
    /// </summary>
    public readonly int Id = s_next_id++;
    /// <summary>The <see cref="Id"/> for the next equation.</summary>
    private static int s_next_id = 0;

    /// <summary>The cells, sorted ascending; only the first <see cref="_count"/> entries are used.</summary>
    private int[] _cells;
    /// <summary>The number of used entries in <see cref="_cells"/>.</summary>
    private int _count;
}

/// <summary>
/// Compares equations by identity, like <see cref="System.Collections.Generic.ReferenceEqualityComparer"/>, but
/// hashes the <see cref="Equation.Id"/> instead of asking the runtime for an object hash code (expensive
/// the first time for every new object).
/// </summary>
public sealed class EquationIdentity : IEqualityComparer<Equation>
{
    /// <summary>The shared instance.</summary>
    public static readonly EquationIdentity Instance = new();

    /// <summary>
    /// Whether <paramref name="x"/> and <paramref name="y"/> are the same object.
    /// </summary>
    /// <param name="x">The first equation.</param>
    /// <param name="y">The second equation.</param>
    /// <returns><c>true</c> if both refer to the same equation.</returns>
    public bool Equals(Equation? x, Equation? y) => ReferenceEquals(x, y);

    /// <summary>
    /// Returns the <see cref="Equation.Id"/> of <paramref name="equation"/>.
    /// </summary>
    /// <param name="equation">The equation to hash.</param>
    /// <returns>The equation's <see cref="Equation.Id"/>.</returns>
    public int GetHashCode(Equation equation) => equation.Id;
}
