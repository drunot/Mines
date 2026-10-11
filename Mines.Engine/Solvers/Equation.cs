using System.Collections.Generic;
using System;

namespace Mines.Solving;

// "The bombs among these cells add up to sum." Cells are Field.Index values, kept sorted in a
// small array: equations rarely have more than 8 cells, so scanning beats hashing.
public class Equation : IEquatable<Equation>
{
    public Equation(int capacity = 8)
    {
        sum = 0;
        _cells = new int[capacity];
    }

    public Equation(Equation equation)
    {
        sum = equation.sum;
        _cells = equation.Cells.ToArray();
        _count = equation._count;
    }

    public int sum;

    public int Count => _count;

    // The cells in ascending order. Don't keep it while changing this equation.
    public ReadOnlySpan<int> Cells => _cells.AsSpan(0, _count);

    public int First => _cells[0];

    // A copy of the cells, for looping while the equation changes.
    public int[] ToArray() => Cells.ToArray();

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

    public bool Contains(int cell) => Array.BinarySearch(_cells, 0, _count, cell) >= 0;

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

    // Is every cell of this equation also in other? Walks both sorted arrays side by side.
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

    // This equation without the cells of smaller (expected to be a subset of it).
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

    public bool Equals(Equation? other) => other is not null && sum == other.sum && Cells.SequenceEqual(other.Cells);
    public override bool Equals(object? other) => other is Equation equation && Equals(equation);
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

    public static bool operator ==(Equation left, Equation right) => left.Equals(right);
    public static bool operator !=(Equation left, Equation right) => !left.Equals(right);

    // Bit r is set while this equation waits in EquationList's queue for rule r.
    internal byte QueuedRules;

    // Unique per equation; a cheap identity hash for EquationIdentity.
    public readonly int Id = s_next_id++;
    private static int s_next_id = 0;

    private int[] _cells;
    private int _count;
}

// Compares equations by identity, like ReferenceEqualityComparer, but hashes the Id instead of
// asking the runtime for an object hash code (expensive the first time for every new object).
public sealed class EquationIdentity : IEqualityComparer<Equation>
{
    public static readonly EquationIdentity Instance = new();

    public bool Equals(Equation? x, Equation? y) => ReferenceEquals(x, y);

    public int GetHashCode(Equation equation) => equation.Id;
}
