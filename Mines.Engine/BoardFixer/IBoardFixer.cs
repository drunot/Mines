namespace Mines.BoardFixer;

using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

/// <summary>
/// Changes the bomb layout of a board the solver got stuck on, so it may become solvable.
/// </summary>
public interface IBoardFixer
{
    /// <summary>
    /// Tries to move bombs on <paramref name="board"/> so the solver can get further.
    /// </summary>
    /// <param name="board">The partly solved board; it may be changed.</param>
    /// <param name="fixedBoard">
    /// The new bomb layout (<c>[row, column]</c> is true for a bomb), or <c>null</c> on failure.
    /// </param>
    /// <returns><c>true</c> if a new layout was produced.</returns>
    bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixedBoard);

    /// <summary>
    /// Works out which bombs to move on <paramref name="board"/> so the solver can get further, without
    /// changing the board.
    /// </summary>
    /// <remarks>
    /// Each pair is one bomb and one free field; swapping them moves the bomb. The order within a pair
    /// doesn't matter (<see cref="FieldMatrix.SwapBombs"/> swaps whichever way round), and the pairs can be
    /// applied in any order.
    /// </remarks>
    /// <param name="board">The partly solved board the solver got stuck on. Its flags must be correct.</param>
    /// <param name="swaps">The pairs of fields (X is the row, Y the column) to swap, or <c>null</c> on failure.</param>
    /// <param name="locked">
    /// Cells whose bomb must not move (<c>[row, column]</c> is true), e.g. what the player of an infinite board has
    /// seen; <c>null</c> to allow every cell.
    /// </param>
    /// <returns><c>true</c> if swaps were found.</returns>
    bool GetBoardChanges(FieldMatrix board, [NotNullWhen(true)] out List<(Point<long>, Point<long>)>? swaps,
                         bool[,]? locked = null);

}
