// No-guess board generation: DeterministicGenerator and FastDeterministicGenerator (solver + EdgeBoardFixer).
// Board i is always generated with new Random(i) and the first click in the centre, so every
// run generates the same boards and results stay comparable while optimizing.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Mines;
using Mines.Generators;
using Mines.Solving;

[MemoryDiagnoser]
public class GeneratorBench
{
    [Params("9x16/20", "16x30/99", "100x100/2000")]
    public string Board = "";

    [Params(nameof(DeterministicGenerator), nameof(FastDeterministicGenerator))]
    public string Generator = "";

    private int _rows;
    private int _columns;
    private int _bombs;
    private int _count;

    [GlobalSetup]
    public void Setup()
    {
        (_rows, _columns, _bombs) = Boards.Parse(Board);
        _count = GeneratorCheck.BoardCount(_rows, _columns);
    }

    // Generates _count boards per operation; divide Mean by that count for the time per board.
    [Benchmark]
    public int Generate()
    {
        int result = 0;
        for (int seed = 0; seed < _count; ++seed)
        {
            bool[,] bombs = GeneratorCheck.Generate(Generator, _rows, _columns, _bombs, seed);
            result += bombs.Length;
        }
        return result;
    }
}

public static class GeneratorCheck
{
    // Fewer large boards, so one benchmark run doesn't take minutes.
    public static int BoardCount(int rows, int columns) => rows * columns > 1000 ? 5 : 50;

    public static Point<int> FirstClick(int rows, int columns) => new Point<int>(rows / 2, columns / 2);

    // The generators to compare, by name.
    public static readonly (string Name, Func<Random, IMatrixGenerator> Create)[] Generators =
    {
        (nameof(DeterministicGenerator), random => new DeterministicGenerator(random)),
        (nameof(FastDeterministicGenerator), random => new FastDeterministicGenerator(random)),
    };

    public static bool[,] Generate(string generator_name, int rows, int columns, int bombs, int seed)
    {
        IMatrixGenerator generator = Generators.Single(g => g.Name == generator_name).Create(new Random(seed));
        return generator.Generate(rows, columns, FirstClick(rows, columns), bombs);
    }

    // Generates boards one by one with each generator, times each, and checks that every board is valid.
    public static void Run(string board, int count, TimeSpan time_limit)
    {
        foreach (var (name, _) in Generators)
        {
            Run(name, board, count, time_limit);
        }
    }

    private static void Run(string generator_name, string board, int count, TimeSpan time_limit)
    {
        var (rows, columns, bomb_count) = Boards.Parse(board);
        Point<int> first = FirstClick(rows, columns);
        var times = new System.Collections.Generic.List<double>();
        int wrong_count = 0, start_not_free = 0, not_solvable = 0;

        for (int seed = 0; seed < count; ++seed)
        {
            var watch = Stopwatch.StartNew();
            Task<bool[,]> task = Task.Run(() => Generate(generator_name, rows, columns, bomb_count, seed));
            if (!task.Wait(time_limit))
            {
                Console.WriteLine($"  {generator_name} {board} seed {seed}: no board after {time_limit.TotalSeconds:F0} s, stopping");
                Environment.Exit(1);
            }
            times.Add(watch.Elapsed.TotalMilliseconds);
            bool[,] bombs = task.Result;

            if (bombs.Cast<bool>().Count(b => b) != bomb_count)
            {
                wrong_count++;
            }
            if (StartHasBomb(bombs, first))
            {
                start_not_free++;
            }
            if (!SolvableFromFirstClick(bombs, first))
            {
                not_solvable++;
            }
        }

        times.Sort();
        Console.WriteLine($"{generator_name} {board}: {count} boards, per board: mean {times.Average():F1} ms, "
            + $"median {times[times.Count / 2]:F1} ms, max {times[^1]:F1} ms | "
            + $"wrong bomb count {wrong_count}, bomb in start area {start_not_free}, "
            + $"not solvable by ConstraintSolver {not_solvable}");
    }

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

    // A fresh board from the layout, first click, then the solver: must end fully solved.
    private static bool SolvableFromFirstClick(bool[,] bombs, Point<int> first)
    {
        var matrix = new FieldMatrix(bombs);
        matrix[first.X, first.Y].Hidden = false;
        new ConstraintSolver().Solve(matrix);
        return matrix.GameWon;
    }
}
