using System;

using System.Linq;

namespace Mines.Generators;


/// <summary>
/// Places the bombs at random, like classic Minesweeper, keeping the first revealed field free.
/// </summary>
public class ClassicRandomGenerator : IMatrixGenerator
{

    /// <summary>
    /// Creates the generator.
    /// </summary>
    /// <param name="random">The random number source; a new <see cref="Random"/> if <c>null</c>.</param>
    public ClassicRandomGenerator(Random? random = null)
    {
        _rand = random ?? new Random();
    }

    /// <summary>The random number source.</summary>
    Random _rand;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">There are not enough cells for <paramref name="bomb_count"/> bombs.</exception>
    public bool[,] Generate(int rows, int columns, Point<int> first_reveal, int bomb_count)
    {
        var bombs = new bool[rows, columns];

        int[] cells = Enumerable.Range(0, rows * columns).ToArray();
        _rand.Shuffle(cells);
        int placed = 0;
        for (int j = 0; j < cells.Length && placed < bomb_count; ++j)
        {
            var point = new Point<int>(cells[j] / columns, cells[j] % columns);
            if (point.Equals(first_reveal))
            {
                continue;   // keep the start point free. (Original Window 2000 Minesweeper moves the first bomb if it is where the user clicks.)
            }
            bombs[point.X, point.Y] = true;
            ++placed;
        }
        if (placed < bomb_count)
        {
            throw new ArgumentException("Not enough cells outside the start area for that many bombs.");
        }

        return bombs;
    }
}