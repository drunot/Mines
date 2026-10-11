using System.Collections.Generic;
using System.Linq;

namespace Mines.Solving;


/// <summary>
/// Compares one equation with the equations that are subsets or supersets of it and adds their
/// differences as new equations.
/// </summary>
class SubsetStrategy : SolvingStrategyBase
{
    /// <summary>
    /// Takes the next changed equation and runs <see cref="EquationList.RemoveSubset(Equation)"/> on it.
    /// </summary>
    /// <param name="fieldMatrix">The board (not changed by this strategy).</param>
    /// <param name="equations">The equations to take from and add to.</param>
    /// <returns><c>true</c> if an equation was taken; <c>false</c> if none was waiting.</returns>
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