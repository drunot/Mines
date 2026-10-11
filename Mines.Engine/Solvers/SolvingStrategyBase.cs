using System.Collections.Generic;
using System.Linq;


namespace Mines.Solving;

abstract class SolvingStrategyBase : ISolvingStrategy
{

    public abstract bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);

    // Equations for a revealed cell, and for every cell its cascade revealed: the walk continues
    // through cells without neighbouring bombs. Visited cells are collected in `cells`.
    protected static List<Equation> createEquations(FieldMatrix fieldMatrix, int location, HashSet<int> cells)
    {
        var equations = new List<Equation>();
        var to_visit = new Stack<int>();
        to_visit.Push(location);
        while (to_visit.TryPop(out int current))
        {
            Field field = fieldMatrix.FieldAt(current);
            if (field.Hidden || !cells.Add(current))
            {
                continue;
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
                    equation.sum += neighbor.Flagged ? 0 : 1;
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

    protected void Flag(FieldMatrix fieldMatrix, EquationList equations, int cell)
    {
        fieldMatrix.FieldAt(cell).Flagged = true;
        equations.MarkBomb(cell);
    }

    protected void Reveal(FieldMatrix fieldMatrix, EquationList equations, int cell)
    {
        Field field = fieldMatrix.FieldAt(cell);
        if (!field.Hidden)
        {
            return;   // already revealed by an earlier cascade, which created its equations too
        }
        field.Hidden = false;

        var revealed_cells = new HashSet<int>();
        foreach (Equation new_equation in createEquations(fieldMatrix, cell, revealed_cells))
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
