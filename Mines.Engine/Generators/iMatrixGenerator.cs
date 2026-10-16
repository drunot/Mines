namespace Mines.Generators;


/// <summary>
/// Places the bombs of a new board.
/// </summary>
public interface IMatrixGenerator
{
    /// <summary>
    /// Places the bombs on <paramref name="matrix"/> (through <see cref="FieldMatrix.ApplyBombs"/>).
    /// </summary>
    /// <param name="matrix">The board to place the bombs on; its size decides the layout's size.</param>
    /// <param name="first_reveal">The first field the player reveals (X is the row, Y the column).</param>
    /// <param name="bomb_count">The number of bombs to place.</param>
    void Generate(FieldMatrix matrix, Point<int> first_reveal, int bomb_count);
}