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
            if (equation.Count == equation.sum)
            {
                foreach (int cell in equation.ToArray())   // copy: MarkBomb changes this equation
                {
                    Flag(fieldMatrix, equations, cell);
                }
            }
            else if (equation.sum == 0)
            {
                foreach (int cell in equation.ToArray())
                {
                    Reveal(fieldMatrix, equations, cell);
                }
            }
            return true;
        }
        return false;
    }

}