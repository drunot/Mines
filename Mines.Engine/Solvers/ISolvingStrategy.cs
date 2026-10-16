namespace Mines.Solving;

/// <summary>
/// One deduction step of <see cref="ConstraintSolver"/>.
/// </summary>
interface ISolvingStrategy
{
    /// <summary>
    /// Applies the strategy once.
    /// </summary>
    /// <param name="fieldMatrix">The board to reveal and flag fields on.</param>
    /// <param name="equations">The solver's equations, updated with what was deduced.</param>
    /// <returns><c>true</c> if the strategy did some work; <c>false</c> if it had nothing left to do.</returns>
    bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);
}
