namespace Mines.Solving;

/// <summary>
/// Looks at one equation at a time: if its sum equals its number of cells all are bombs, if its sum
/// is 0 all are safe.
/// </summary>
class NeighborStrategy : SolvingStrategyBase
{
    /// <summary>
    /// Takes the next changed equation and flags or reveals all its cells if it decides them.
    /// </summary>
    /// <param name="fieldMatrix">The board to change.</param>
    /// <param name="equations">The equations to take from and update.</param>
    /// <returns>
    /// <c>true</c> if an equation was taken (even if it decided nothing); <c>false</c> if none was waiting.
    /// </returns>
    public override bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations)
    {
        // Could still probably be one.
        if (equations.TryTakeChanged(Rule.Neighbor, out Equation equation))
        {
            if (equation.Count == equation.Sum)
            {
                foreach (int cell in equation.ToArray())   // copy: MarkBomb changes this equation
                {
                    Flag(fieldMatrix, equations, cell);
                }
            }
            else if (equation.Sum == 0)
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
