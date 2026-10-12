

using System;
using System.Collections.Generic;

namespace Mines.Solving;

/// <summary>
/// A solver that can move bombs on the board it last solved and carry on from where it got stuck, instead of
/// starting again from the first reveal.
/// </summary>
public interface IResumableSolver: ISolver
{
    /// <summary>
    /// Swaps bombs and free fields on the board of the last <see cref="ISolver.Solve"/> call and continues
    /// solving it. The revealed and flagged fields are kept.
    /// </summary>
    /// <remarks>
    /// Some kept fields may have been deduced from numbers the swaps changed, so a board solved this way is
    /// not always solvable from the first reveal. Check the final layout from scratch before relying on it.
    /// </remarks>
    /// <param name="swaps">
    /// The pairs of fields to swap (X is the row, Y the column), e.g. from
    /// <see cref="BoardFixer.iBoardFixer.GetBoardChanges"/>. Both fields of a pair must be hidden and unflagged.
    /// </param>
    void SwapAndContinue(List<(Point<int>,Point<int>)> swaps);

}