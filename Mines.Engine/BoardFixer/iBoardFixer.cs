
namespace Mines.BoardFixer;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Changes the bomb layout of a board the solver got stuck on, so it may become solvable.
/// </summary>
public interface iBoardFixer
{
    /// <summary>
    /// Tries to move bombs on <paramref name="board"/> so the solver can get further.
    /// </summary>
    /// <param name="board">The partly solved board; it may be changed.</param>
    /// <param name="fixed_board">The new bomb layout (<c>[row, column]</c> is true for a bomb), or <c>null</c> on failure.</param>
    /// <returns><c>true</c> if a new layout was produced.</returns>
    bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixed_board);
}