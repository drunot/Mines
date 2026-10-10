using System.Collections.Generic;
using System.Linq;


namespace Mines.Solving;

abstract class SolvingStrategyBase: ISolvingStrategy
{

    public abstract bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);
    protected static List<Equation> createEquations(FieldMatrix fieldMatrix, Point<int> location, HashSet<Point<int>> points)
    {

        if (points.Contains(location) || fieldMatrix[location.X, location.Y].Hidden)
        {
            return new List<Equation>();
        }
        points.Add(location);
        List<Equation> equations = new List<Equation>();
        int number = 0;
        var eq = fieldMatrix[location.X, location.Y].Neighbors.Aggregate(new Equation(), (Equation e, Field f) =>
            {
                if (f.Hidden && !f.Flagged)
                {
                    e.variables.Add(f.Position);
                }
                if (f.IsBomb)
                {
                    e.sum += f.Flagged ? 0 : 1; // equals f.Number - Flagged, counted here to avoid a second pass.
                    number += 1;  // equals f.Number, counted here to avoid a second pass.
                }
                return e;
            });
        if (number != 0)
        {
            if (eq.variables.Count != 0) equations.Add(eq);
            return equations;
        }
        fieldMatrix[location.X, location.Y].Neighbors.Aggregate(equations, (List<Equation> e, Field f) => { e.AddRange(createEquations(fieldMatrix, f.Position, points)); return e; });
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