using System;

namespace Mines;

/// <summary>
/// The size of a finite board.
/// </summary>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
public readonly record struct BoardSize(int Rows, int Columns);

/// <summary>
/// A board the game can be played on: a finite <see cref="FieldMatrix"/> or an endless
/// <see cref="InfiniteFieldMatrix"/>. The view draws and plays any board through this interface.
/// </summary>
/// <remarks>
/// Positions are global: on a finite board they run from (0, 0) to (<c>Rows - 1</c>, <c>Columns - 1</c>); on an
/// infinite board they can be anything, also negative. The game rules (the first click, chording, the cascade)
/// are applied by the board, not by the view.
/// </remarks>
public interface IBoard
{
    /// <summary>The size of the board, or <c>null</c> for an infinite board.</summary>
    BoardSize? Size { get; }

    /// <summary>Whether the first cell has been revealed (and the bombs placed).</summary>
    bool Initialized { get; }

    /// <summary>Whether the game is lost. An infinite board is never lost: a bomb only resets its streak.</summary>
    bool GameLost { get; }

    /// <summary>Whether the game is won. An infinite board can't be won.</summary>
    bool GameWon { get; }

    /// <summary>Raised when fields change: revealed, flagged or marked.</summary>
    event EventHandler? FieldChanged;

    /// <summary>
    /// Gets the field at a position.
    /// </summary>
    /// <param name="row">The global row.</param>
    /// <param name="column">The global column.</param>
    /// <returns>
    /// The field, or <c>null</c> if there is none: outside a finite board, or a part of an infinite board that
    /// hasn't been generated yet (draw it as hidden).
    /// </returns>
    Field? FieldAt(long row, long column);

    /// <summary>
    /// The player reveals a cell: the first click places the bombs, a cell without neighbouring bombs opens its
    /// neighbours, and a revealed number whose bombs are all flagged chords (reveals its other neighbours). On
    /// an infinite board, bombs that were already revealed count as flagged for a chord.
    /// </summary>
    /// <param name="row">The global row.</param>
    /// <param name="column">The global column.</param>
    void Reveal(long row, long column);

    /// <summary>
    /// The player marks a hidden cell: none, then a flag, then a question mark, then none again.
    /// </summary>
    /// <param name="row">The global row.</param>
    /// <param name="column">The global column.</param>
    void CycleMark(long row, long column);

    /// <summary>
    /// Tells the board which cells are on screen. An infinite board only lets a cascade open cells in and near
    /// this area, and continues it when the area moves; a finite board ignores it.
    /// </summary>
    /// <param name="top">The first visible row.</param>
    /// <param name="left">The first visible column.</param>
    /// <param name="bottom">The last visible row.</param>
    /// <param name="right">The last visible column.</param>
    void SetVisibleArea(long top, long left, long bottom, long right);
}
