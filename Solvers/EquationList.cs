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
        _global = new Equation();
        foreach (Field f in _matrix.Fields)
        {
            if (f.Hidden && !f.Flagged)
            {
                _global.variables.Add(f.Position);
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
        _equation_record = new List<Equation>[_matrix.Rows, _matrix.Columns];
    }

    FieldMatrix _matrix;

    // New equation: store it, list it under its cells, and queue it.
    public void Add(Equation equation)
    {
        if (equation.variables.Count == 0)
        {
            return;
        }
        var first = equation.variables.First();
        if (_equation_record[first.X, first.Y]?.Contains(equation) == true)
        {
            return;   // an equal equation already exists
        }

        _equations.Add(equation);
        foreach (var variable in equation.variables)
        {
            _equation_record[variable.X, variable.Y] ??= new List<Equation>();
            _equation_record[variable.X, variable.Y].Add(equation);
        }
        MarkChanged(equation);
    }

    // A revealed cell is safe: drop it from every equation, the sums stay the same.
    public void MarkSafe(Point<int> point) => RemoveVariable(point, 0);

    // A flagged cell is a bomb: drop it from every equation and lower their sums.
    public void MarkBomb(Point<int> point) => RemoveVariable(point, 1);

    // The equations that contain this cell, for the subset step.
    public IReadOnlyList<Equation> Containing(Point<int> point)
    {
        return (IReadOnlyList<Equation>?)_equation_record[point.X, point.Y] ?? Array.Empty<Equation>();
    }

    public bool TryTakeChanged(Rule rule, out Equation equation)
    {
        int r = (int)rule;
        while (_pending[r].TryDequeue(out equation!))
        {
            if (!_queued[r].Remove(equation))
            {
                continue;   // removed from the queue earlier, e.g. as part of a group
            }
            if (equation.variables.Count != 0)
            {
                return true;
            }
        }
        return false;
    }

    private void RemoveVariable(Point<int> point, int bombs)
    {
        List<Equation>? containing = _equation_record[point.X, point.Y];
        if (containing is null)
        {
            return;
        }
        foreach (Equation equation in containing)
        {
            equation.variables.Remove(point);
            equation.sum -= bombs;
            if (equation.variables.Count == 0)
            {
                _equations.Remove(equation);
            }
            MarkChanged(equation);   // it lost a cell, so the rules may now apply to it
        }
        containing.Clear();
    }

    public void RemoveSubset(Equation equation)
    {
        // Ignore Global for this.
        if (equation.variables.Count == 0 || ReferenceEquals(equation, _global))
        {
            return;
        }

        var candidates = new HashSet<Equation>(ReferenceEqualityComparer.Instance);
        foreach (var variable in equation.variables)
        {
            candidates.UnionWith(_equation_record[variable.X, variable.Y]);
        }
        candidates.Remove(equation);
        if (_global is not null)
        {
            candidates.Remove(_global);
        }
        var differences = new List<Equation>();
        foreach (Equation test_equation in candidates)
        {
            if (test_equation.variables.Count == 0)
            {
                continue;
            }
            if (test_equation.variables.IsSubsetOf(equation.variables))
            {
                differences.Add(Difference(equation, test_equation));
            }
            else if (equation.variables.IsSubsetOf(test_equation.variables))
            {
                differences.Add(Difference(test_equation, equation));
            }
        }
        foreach (Equation difference in differences)
        {
            Add(difference);
        }
    }

    private static Equation Difference(Equation bigger, Equation smaller)
    {
        var difference = new Equation();
        difference.variables = [.. bigger.variables];
        difference.variables.ExceptWith(smaller.variables);
        difference.sum = bigger.sum - smaller.sum;
        return difference;
    }
    // Next equation that changed since it was last taken, skipping solved ones.
    private void MarkChanged(Equation equation)
    {
        for (int rule = 0; rule < _pending.Length; ++rule)
        {
            if (_queued[rule].Add(equation))
            {
                _pending[rule].Enqueue(equation);
            }
        }
    }

    private readonly HashSet<Equation> _equations = new(ReferenceEqualityComparer.Instance);
    private readonly List<Equation>[,] _equation_record;
    private readonly Queue<Equation>[] _pending = { new(), new(), new() };
    private readonly HashSet<Equation>[] _queued =
    {
        new(ReferenceEqualityComparer.Instance),
        new(ReferenceEqualityComparer.Instance),
        new(ReferenceEqualityComparer.Instance),
    };

    const int MaxCellsInGroup = 40; // Could be encased later.

    private HashSet<Equation> GetGroupFromEquation(Equation equation, out HashSet<Point<int>> cells)
    {
        HashSet<Equation> equations = new HashSet<Equation>(ReferenceEqualityComparer.Instance);
        HashSet<Point<int>> local_cells = [.. equation.variables];
        var checked_points = new HashSet<Point<int>>();
        equations.Add(equation);
        var to_visit = new Queue<Point<int>>(equation.variables);
        while (to_visit.TryDequeue(out Point<int>? v))
        {
            if (!checked_points.Add(v))
            {
                continue;   // already visited
            }
            foreach (Equation e in _equation_record[v.X, v.Y])
            {
                if (ReferenceEquals(e, _global) || equations.Contains(e))
                {
                    continue;   // the global equation, or already in the group
                }
                int new_cells = e.variables.Count(p => !local_cells.Contains(p));
                if (local_cells.Count + new_cells > MaxCellsInGroup)
                {
                    continue;   // doesn't fit; it stays queued and can start its own piece later
                }
                equations.Add(e);
                local_cells.UnionWith(e.variables);
                _queued[(int)Rule.Enumeration].Remove(e);
                foreach (Point<int> next in e.variables)
                {
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
        [NotNullWhen(true)] out HashSet<Point<int>>? cells)
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
        [NotNullWhen(true)] out HashSet<Point<int>>? cells)
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