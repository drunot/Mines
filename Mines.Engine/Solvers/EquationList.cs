using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Mines.Solving;


public enum Rule { Neighbor = 0, Subset = 1, Enumeration = 2 }

public class EquationList
{

    const int FieldsBeforeLast = 20;
    public void TryCreateGlobalEquation()
    {
        if (_global is not null || _matrix.FieldsLeft > FieldsBeforeLast)
        {
            return;
        }
        _global = new Equation(Math.Max(1, _matrix.FieldsLeft));
        foreach (Field f in _matrix.Fields)
        {
            if (f.Hidden && !f.Flagged)
            {
                _global.Add(f.Index);
            }
        }
        _global.sum = _matrix.BombsLeft;
        Add(_global);
    }

    public Equation? Global
    {
        get => _global;
    }

    Equation? _global = null;

    public EquationList(FieldMatrix matrix)
    {
        _matrix = matrix;
        _equation_record = new List<Equation>?[_matrix.Rows * _matrix.Columns];
    }

    FieldMatrix _matrix;

    // New equation: store it, list it under its cells, and queue it.
    public void Add(Equation equation)
    {
        if (equation.Count == 0)
        {
            return;
        }
        if (_equation_record[equation.First]?.Contains(equation) == true)
        {
            return;   // an equal equation already exists
        }

        foreach (int cell in equation.Cells)
        {
            (_equation_record[cell] ??= new List<Equation>()).Add(equation);
        }
        MarkChanged(equation);
    }

    // A revealed cell is safe: drop it from every equation, the sums stay the same.
    public void MarkSafe(int cell) => RemoveVariable(cell, 0);

    // A flagged cell is a bomb: drop it from every equation and lower their sums.
    public void MarkBomb(int cell) => RemoveVariable(cell, 1);

    // The equations that contain this cell, for the subset step.
    public IReadOnlyList<Equation> Containing(int cell)
    {
        return (IReadOnlyList<Equation>?)_equation_record[cell] ?? Array.Empty<Equation>();
    }

    public bool TryTakeChanged(Rule rule, out Equation equation)
    {
        int r = (int)rule;
        while (_pending[r].TryDequeue(out equation!))
        {
            byte bit = (byte)(1 << r);
            if ((equation.QueuedRules & bit) == 0)
            {
                continue;   // removed from the queue earlier, e.g. as part of a group
            }
            equation.QueuedRules &= (byte)~bit;
            if (equation.Count != 0)
            {
                return true;
            }
        }
        return false;
    }

    private void RemoveVariable(int cell, int bombs)
    {
        List<Equation>? containing = _equation_record[cell];
        if (containing is null)
        {
            return;
        }
        foreach (Equation equation in containing)
        {
            equation.Remove(cell);
            equation.sum -= bombs;
            MarkChanged(equation);   // it lost a cell, so the rules may now apply to it
        }
        containing.Clear();
    }

    public void RemoveSubset(Equation equation)
    {
        // Ignore Global for this.
        if (equation.Count == 0 || ReferenceEquals(equation, _global))
        {
            return;
        }

        var candidates = new HashSet<Equation>(EquationIdentity.Instance);
        foreach (int cell in equation.Cells)
        {
            List<Equation>? containing = _equation_record[cell];
            if (containing is not null)
            {
                candidates.UnionWith(containing);
            }
        }
        candidates.Remove(equation);
        if (_global is not null)
        {
            candidates.Remove(_global);
        }
        var differences = new List<Equation>();
        foreach (Equation test_equation in candidates)
        {
            if (test_equation.Count == 0)
            {
                continue;
            }
            if (test_equation.IsSubsetOf(equation))
            {
                differences.Add(equation.Minus(test_equation));
            }
            else if (equation.IsSubsetOf(test_equation))
            {
                differences.Add(test_equation.Minus(equation));
            }
        }
        foreach (Equation difference in differences)
        {
            Add(difference);
        }
    }

    // Queue the equation for every rule; it changed, so each rule may now apply to it.
    private void MarkChanged(Equation equation)
    {
        for (int rule = 0; rule < _pending.Length; ++rule)
        {
            byte bit = (byte)(1 << rule);
            if ((equation.QueuedRules & bit) == 0)   // not already waiting for this rule
            {
                equation.QueuedRules |= bit;
                _pending[rule].Enqueue(equation);
            }
        }
    }

    private readonly List<Equation>?[] _equation_record;   // cell index -> equations containing it
    private readonly Queue<Equation>[] _pending = { new(), new(), new() };

    const int MaxCellsInGroup = 40; // Could be encased later.

    private HashSet<Equation> GetGroupFromEquation(Equation equation, out HashSet<int> cells)
    {
        HashSet<Equation> equations = new HashSet<Equation>(EquationIdentity.Instance);
        var local_cells = new HashSet<int>(equation.ToArray());
        var checked_cells = new HashSet<int>();
        equations.Add(equation);
        var to_visit = new Queue<int>(equation.ToArray());
        while (to_visit.TryDequeue(out int v))
        {
            if (!checked_cells.Add(v))
            {
                continue;   // already visited
            }
            List<Equation>? containing = _equation_record[v];
            if (containing is null)
            {
                continue;
            }
            foreach (Equation e in containing)
            {
                if (ReferenceEquals(e, _global) || equations.Contains(e))
                {
                    continue;   // the global equation, or already in the group
                }
                int new_cells = 0;
                foreach (int c in e.Cells)
                {
                    new_cells += local_cells.Contains(c) ? 0 : 1;
                }
                if (local_cells.Count + new_cells > MaxCellsInGroup)
                {
                    continue;   // doesn't fit; it stays queued and can start its own piece later
                }
                equations.Add(e);
                e.QueuedRules &= unchecked((byte)~(1 << (int)Rule.Enumeration));   // covered by this group
                foreach (int next in e.Cells)
                {
                    local_cells.Add(next);
                    to_visit.Enqueue(next);
                }
            }
        }
        cells = local_cells;
        return equations;
    }

    private bool _global_pending = false;

    public bool TryTakeEnumerationGroup(
        [NotNullWhen(true)] out HashSet<Equation>? equations,
        [NotNullWhen(true)] out HashSet<int>? cells)
    {
        Equation equation;

        // Due to return in the bottom it will at most run twice and only if _global is the equation.
        while (TryTakeChanged(Rule.Enumeration, out equation))
        {
            if (ReferenceEquals(equation, _global))
            {
                _global_pending = true;   // the global group needs enumerating again
                continue; // Skip global as stating equation.
            }

            equations = GetGroupFromEquation(equation, out cells);

            return true;
        }
        equations = null;
        cells = null;
        return false;
    }

    public bool TryTakeGlobalGroup(
        [NotNullWhen(true)] out HashSet<Equation>? equations,
        [NotNullWhen(true)] out HashSet<int>? cells)
    {
        if (_global_pending && _global is not null)
        {
            _global_pending = false;
            equations = GetGroupFromEquation(_global, out cells);
            return true;
        }
        equations = null;
        cells = null;
        return false;
    }
}
