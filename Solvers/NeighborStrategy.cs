using System.Collections.Generic;
using System.Linq;

namespace Mines.Solving;


class NeighborStrategy : SolvingStrategyBase
{
    public override bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations)
    {
        // Could still probably be one.
        if (equations.TryTakeChanged(Rule.Neighbor, out Equation equation))
        {
            if (equation.variables.Count == equation.sum)
            {
                foreach (Point<int> variable in equation.variables.ToArray())   // copy: MarkBomb changes this set
                {
                    Flag(fieldMatrix, equations, variable);
                }
            }
            else if (equation.sum == 0)
            {
                foreach (Point<int> variable in equation.variables.ToArray())
                {
                    Reveal(fieldMatrix, equations, variable);
                }
            }
            return true;
        }
        return false;
    }

}