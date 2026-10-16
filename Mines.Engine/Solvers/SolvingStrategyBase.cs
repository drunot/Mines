using System.Collections.Generic;
using System.Linq;


namespace Mines.Solving;

/// <summary>
/// Base class for the solving strategies, with helpers to flag and reveal cells while keeping the
/// <see cref="EquationList"/> up to date.
/// </summary>
abstract class SolvingStrategyBase : ISolvingStrategy
{

    /// <inheritdoc/>
    public abstract bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);

    /// <summary>
    /// Equations for a revealed cell, and for every cell its cascade revealed: the walk continues
    /// through cells without neighbouring bombs. Visited cells are collected in <paramref name="cells"/>.
    /// </summary>
    /// <param name="fieldMatrix">The board to read.</param>
    /// <param name="list">The equation list, which knows which revealed cells give an equation.</param>
    /// <param name="location">The <see cref="Field.Index"/> of the revealed cell to start from.</param>
    /// <param name="cells">Receives the revealed cells visited; cells already in it are skipped.</param>
    /// <returns>The non-empty equations of the visited cells that have neighbouring bombs.</returns>
    protected static List<Equation> createEquations(FieldMatrix fieldMatrix, EquationList list, int location, HashSet<int> cells)
    {
        var equations = new List<Equation>();
        var to_visit = new Stack<int>();
        to_visit.Push(location);
        while (to_visit.TryPop(out int current))
        {
            Field field = fieldMatrix.FieldAt(current);
            if (field.Hidden || !cells.Add(current) || !list.GivesEquation(field))
            {
                continue;   // (a cell that shows no number also opens nothing)
            }

            var equation = new Equation();
            int number = 0;
            foreach (Field neighbor in field.Neighbors)
            {
                if (neighbor.Hidden && !neighbor.Flagged)
                {
                    equation.Add(neighbor.Index);
                }
                if (neighbor.IsBomb)
                {
                    equation.sum += neighbor.Hidden && !neighbor.Flagged ? 1 : 0;   // flags and revealed bombs are known
                    number += 1;
                }
            }

            if (number != 0)
            {
                if (equation.Count != 0)
                {
                    equations.Add(equation);
                }
                continue;
            }
            foreach (Field neighbor in field.Neighbors)
            {
                to_visit.Push(neighbor.Index);
            }
        }
        return equations;
    }

    /// <summary>
    /// Flags <paramref name="cell"/> and removes it from the equations as a bomb.
    /// </summary>
    /// <param name="fieldMatrix">The board to change.</param>
    /// <param name="equations">The equations to update.</param>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    protected void Flag(FieldMatrix fieldMatrix, EquationList equations, int cell)
    {
        fieldMatrix.FieldAt(cell).Flagged = true;
        equations.MarkBomb(cell);
    }

    /// <summary>
    /// Reveals <paramref name="cell"/> (if still hidden), adds the equations of it and of the cells its
    /// cascade revealed, and removes all of them from the equations as safe.
    /// </summary>
    /// <param name="fieldMatrix">The board to change.</param>
    /// <param name="equations">The equations to update.</param>
    /// <param name="cell">The <see cref="Field.Index"/> of the cell.</param>
    protected void Reveal(FieldMatrix fieldMatrix, EquationList equations, int cell)
    {
        Field field = fieldMatrix.FieldAt(cell);
        if (!field.Hidden)
        {
            return;   // already revealed by an earlier cascade, which created its equations too
        }
        field.Hidden = false;

        var revealed_cells = new HashSet<int>();
        foreach (Equation new_equation in createEquations(fieldMatrix, equations, cell, revealed_cells))
        {
            equations.Add(new_equation);
        }
        foreach (int revealed in revealed_cells)
        {
            equations.MarkSafe(revealed);
        }
        equations.MarkSafe(cell);
    }
}
