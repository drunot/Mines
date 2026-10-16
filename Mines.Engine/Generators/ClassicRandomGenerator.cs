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
    /// <exception cref="ArgumentException">
    /// There are not enough cells for <paramref name="bombCount"/> bombs.
    /// </exception>
    public void Generate(FieldMatrix fieldMatrix, Point<int> firstReveal, int bombCount)
    {
        var bombs = new bool[fieldMatrix.Rows, fieldMatrix.Columns];

        int[] cells = Enumerable.Range(0, fieldMatrix.Rows * fieldMatrix.Columns).ToArray();
        _rand.Shuffle(cells);
        int placed = 0;
        for (int j = 0; j < cells.Length && placed < bombCount; ++j)
        {
            var point = new Point<int>(cells[j] / fieldMatrix.Columns, cells[j] % fieldMatrix.Columns);
            if (point.Equals(firstReveal))
            {
                // keep the start point free. (Original Window 2000 Minesweeper moves the first bomb if it is
                // where the user clicks.)
                continue;
            }
            bombs[point.X, point.Y] = true;
            ++placed;
        }
        if (placed < bombCount)
        {
            throw new ArgumentException("Not enough cells outside the start area for that many bombs.");
        }

        fieldMatrix.ApplyBombs(bombs);
    }
}
