using System.Collections.Generic;
using System.Linq;

namespace Mines.Solving;


class SubsetStrategy : SolvingStrategyBase
{
    public override bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations)
    {
        if (equations.TryTakeChanged(Rule.Subset, out Equation equation))
        {
            equations.RemoveSubset(equation);
            return true;
        }
        return false;
    }
}