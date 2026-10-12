using System;
using System.Linq;
using Mines;
using Mines.Generators;
using Mines.Solving;

namespace Mines.Tests;

// Every board from a no-guess generator must be solvable from the first click alone: a fresh board with
// the final layout, the first click, then the solver. How the generator got there doesn't matter.
public class GeneratorTests
{
    public static TheoryData<string, int, int, int> Cases()
    {
        var data = new TheoryData<string, int, int, int>();
        foreach (string generator in new[] { nameof(DeterministicGenerator), nameof(FastDeterministicGenerator) })
        {
            data.Add(generator, 9, 9, 10);
            data.Add(generator, 9, 16, 20);
            data.Add(generator, 16, 30, 99);
        }
        return data;
    }

    private const int SeedCount = 30;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Boards_are_solvable_from_the_first_click(string generator_name, int rows, int columns, int bomb_count)
    {
        var first = new Point<int>(rows / 2, columns / 2);
        for (int seed = 0; seed < SeedCount; ++seed)
        {
            bool[,] bombs = Create(generator_name, seed).Generate(rows, columns, first, bomb_count);
            string where = $"{generator_name} {rows}x{columns}/{bomb_count} seed {seed}";

            Assert.True(bomb_count == bombs.Cast<bool>().Count(b => b), $"wrong bomb count ({where})");
            Assert.False(StartHasBomb(bombs, first), $"bomb in the start area ({where})");

            var matrix = new FieldMatrix(bombs);
            matrix[first.X, first.Y].Hidden = false;
            new ConstraintSolver().Solve(matrix);
            Assert.True(matrix.GameWon, $"not solvable from the first click ({where})");
        }
    }

    private static IMatrixGenerator Create(string generator_name, int seed) => generator_name switch
    {
        nameof(DeterministicGenerator) => new DeterministicGenerator(new Random(seed)),
        nameof(FastDeterministicGenerator) => new FastDeterministicGenerator(new Random(seed)),
        _ => throw new ArgumentException($"Unknown generator {generator_name}"),
    };

    // The 3x3 area around the first click must be free of bombs.
    private static bool StartHasBomb(bool[,] bombs, Point<int> first)
    {
        for (int r = Math.Max(0, first.X - 1); r <= Math.Min(bombs.GetLength(0) - 1, first.X + 1); ++r)
        {
            for (int c = Math.Max(0, first.Y - 1); c <= Math.Min(bombs.GetLength(1) - 1, first.Y + 1); ++c)
            {
                if (bombs[r, c])
                {
                    return true;
                }
            }
        }
        return false;
    }
}
