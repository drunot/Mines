namespace Mines.Generators;


/// <summary>
/// Places the bombs of a new board.
/// </summary>
public interface IMatrixGenerator
{
    /// <summary>
    /// Creates a bomb layout.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="first_reveal">The first field the player reveals (X is the row, Y the column).</param>
    /// <param name="bomb_count">The number of bombs to place.</param>
    /// <returns>The layout: <c>[row, column]</c> is true for a bomb.</returns>
    bool[,] Generate(int rows, int columns, Point<int> first_reveal, int bomb_count);
}