using System.Collections.Generic;
using System.Linq;


namespace Mines.Solving;

abstract class SolvingStrategyBase : ISolvingStrategy
{

    public abstract bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);
    protected static List<Equation> createEquations(FieldMatrix fieldMatrix, Point<int> location, HashSet<Point<int>> points)
    {
        var equations = new List<Equation>();
        var to_visit = new Stack<Point<int>>();
        to_visit.Push(location);
        while (to_visit.TryPop(out Point<int>? current))
        {
            Field field = fieldMatrix[current.X, current.Y];
            if (field.Hidden || !points.Add(current))
            {
                continue;
            }

            var equation = new Equation();
            int number = 0;
            foreach (Field neighbor in field.Neighbors)
            {
                if (neighbor.Hidden && !neighbor.Flagged)
                {
                    equation.variables.Add(neighbor.Position);
                }
                if (neighbor.IsBomb)
                {
                    equation.sum += neighbor.Flagged ? 0 : 1;
                    number += 1;
                }
            }

            if (number != 0)
            {
                if (equation.variables.Count != 0)
                {
                    equations.Add(equation);
                }
                continue;
            }
            foreach (Field neighbor in field.Neighbors)
            {
                to_visit.Push(neighbor.Position);
            }
        }
        return equations;
    }

    protected void Flag(FieldMatrix fieldMatrix, EquationList equations, Point<int> point)
    {
        fieldMatrix[point.X, point.Y].Flagged = true;
        equations.MarkBomb(point);
    }

    protected void Reveal(FieldMatrix fieldMatrix, EquationList equations, Point<int> point)
    {
        fieldMatrix[point.X, point.Y].Hidden = false;

        var revealed_points = new HashSet<Point<int>>();
        foreach (Equation new_equation in createEquations(fieldMatrix, point, revealed_points))
        {
            equations.Add(new_equation);
        }
        foreach (Point<int> revealed in revealed_points)
        {
            equations.MarkSafe(revealed);
        }
        equations.MarkSafe(point);
    }
}