using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Mines.Solving;


/// <summary>
/// The solving rules that each have their own queue of changed equations in <see cref="EquationList"/>.
/// </summary>
public enum Rule
{
    /// <summary>Single-equation reasoning: all-bombs or all-safe (<see cref="NeighborStrategy"/>).</summary>
    Neighbor = 0,
    /// <summary>Comparing equations where one's cells contain the other's (<see cref="SubsetStrategy"/>).</summary>
    Subset = 1,
    /// <summary>Trying every bomb arrangement in a group of cells (<see cref="EnumerationStrategy"/>).</summary>
    Enumeration = 2,
}

/// <summary>
/// All equations the solver knows about, indexed by cell, with a queue of changed equations per
/// <see cref="Rule"/>.
/// </summary>
public class EquationList
{

    /// <summary>
    /// The global equation (all remaining cells add up to the bombs left) is created once at most this
    /// many fields are left.
    /// </summary>
    const int FieldsBeforeLast = 20;
    /// <summary>
    /// Creates and adds the global equation (every hidden, unflagged cell; sum is
    /// <see cref="FieldMatrix.BombsLeft"/>) once few enough fields are left. Does nothing if it already
    /// exists or more than <see cref="FieldsBeforeLast"/> fields are left.
    /// </summary>
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

    /// <summary>
    /// The global equation over all remaining cells, or <c>null</c> if it hasn't been created yet.
    /// </summary>
    public Equation? Global
    {
        get => _global;
    }

    /// <summary>Backing field for <see cref="Global"/>.</summary>
    Equation? _global = null;

    /// <summary>
    /// Creates an empty equation list for <paramref name="matrix"/>.
    /// </summary>
    /// <param name="matrix">The board the equations describe.</param>
    public EquationList(FieldMatrix matrix)
    {
        _matrix = matrix;
        _equation_record = new List<Equation>?[_matrix.Rows * _matrix.Columns];
    }

    FieldMatrix _matrix;

    /// <summary>
    /// New equation: store it, list it under its cells, and queue it. Empty equations and equations equal
    /// to an existing one are ignored.
    /// </summary>
    /// <param name="equation">The equation to add.</param>
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

    /// <summary>
    /// A revealed cell is safe: drop it from every equation, the sums stay the same.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    public void MarkSafe(int cell) => RemoveVariable(cell, 0);

    /// <summary>
    /// A flagged cell is a bomb: drop it from every equation and lower their sums.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    public void MarkBomb(int cell) => RemoveVariable(cell, 1);

    /// <summary>
    /// The equations that contain this cell, for the subset step.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    /// <returns>The equations containing the cell; empty if there are none.</returns>
    public IReadOnlyList<Equation> Containing(int cell)
    {
        return (IReadOnlyList<Equation>?)_equation_record[cell] ?? Array.Empty<Equation>();
    }

    /// <summary>
    /// Takes the next non-empty equation waiting in the queue for <paramref name="rule"/>.
    /// </summary>
    /// <param name="rule">The rule whose queue to take from.</param>
    /// <param name="equation">The equation taken; undefined when the method returns <c>false</c>.</param>
    /// <returns><c>true</c> if an equation was taken; <c>false</c> if the queue is empty.</returns>
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

    /// <summary>
    /// Drops <paramref name="cell"/> from every equation containing it, lowers their sums by
    /// <paramref name="bombs"/> and queues them again.
    /// </summary>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    /// <param name="bombs">1 if the cell is a bomb, 0 if it is safe.</param>
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

    /// <summary>
    /// Subset step: for every equation that is a subset or superset of <paramref name="equation"/>, adds
    /// the difference of the two as a new equation. The global equation is ignored.
    /// </summary>
    /// <param name="equation">The equation to compare the others against.</param>
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

    /// <summary>
    /// Queue the equation for every rule; it changed, so each rule may now apply to it.
    /// </summary>
    /// <param name="equation">The changed equation.</param>
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

    /// <summary>Cell index -&gt; equations containing it (<c>null</c> if none yet).</summary>
    private readonly List<Equation>?[] _equation_record;   // cell index -> equations containing it
    /// <summary>One queue of changed equations per <see cref="Rule"/>.</summary>
    private readonly Queue<Equation>[] _pending = { new(), new(), new() };

    /// <summary>The largest number of cells a group for the enumeration step may have.</summary>
    const int MaxCellsInGroup = 40; // Could be encased later.

    /// <summary>
    /// Collects the group of equations connected to <paramref name="equation"/> through shared cells, up
    /// to <see cref="MaxCellsInGroup"/> cells, skipping the global equation (unless it is the start).
    /// Equations added to the group are taken out of the enumeration queue.
    /// </summary>
    /// <param name="equation">The equation to start from.</param>
    /// <param name="cells">All cells of the group's equations.</param>
    /// <returns>The equations of the group, compared by identity.</returns>
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

    /// <summary>
    /// Set when the global equation came up in the enumeration queue, so its group needs enumerating again.
    /// </summary>
    private bool _global_pending = false;

    /// <summary>
    /// Takes the group of the next changed equation waiting for <see cref="Rule.Enumeration"/>. The global
    /// equation is never used as the start; it is remembered for <see cref="TryTakeGlobalGroup"/> instead.
    /// </summary>
    /// <param name="equations">The equations of the group.</param>
    /// <param name="cells">The cells of the group.</param>
    /// <returns><c>true</c> if a group was taken; <c>false</c> if the queue is empty.</returns>
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

    /// <summary>
    /// Takes the group of the global equation, if it changed since it was last taken.
    /// </summary>
    /// <param name="equations">The equations of the group.</param>
    /// <param name="cells">The cells of the group.</param>
    /// <returns><c>true</c> if a group was taken; <c>false</c> otherwise.</returns>
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
