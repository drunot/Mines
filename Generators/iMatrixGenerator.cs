namespace Mines.Generators;


public interface IMatrixGenerator
{
    bool[,] Generate(int rows, int columns, Point<int> first_reveal, int bomb_count);
}