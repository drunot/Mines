// No-guess board generation: DeterministicGenerator and FastDeterministicGenerator. Both run ConstraintSolver and
// let EdgeBoardFixer move bombs when it gets stuck; the fast one continues the solver after each fix and solves a
// fixed board once more from the first click before returning it.
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

// Time to generate a fixed set of boards, per generator and board size.
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

// Board generation shared by GeneratorBench and the gencheck command, which also validates every board.
public static class GeneratorCheck
{
    // Fewer large boards, so one benchmark run doesn't take minutes.
    public static int BoardCount(int rows, int columns) => rows * columns > 1000 ? 5 : 50;

    // The first click is always in the centre of the board.
    public static Point<int> FirstClick(int rows, int columns) => new Point<int>(rows / 2, columns / 2);

    // The generators to compare, by name.
    public static readonly (string Name, Func<Random, IMatrixGenerator> Create)[] Generators =
    {
        (nameof(DeterministicGenerator), random => new DeterministicGenerator(random)),
        (nameof(FastDeterministicGenerator), random => new FastDeterministicGenerator(random)),
    };

    // Board number `seed` from the named generator, as a layout: bombs[row, column] is true for a bomb.
    public static bool[,] Generate(string generatorName, int rows, int columns, int bombs, int seed)
    {
        IMatrixGenerator generator = Generators.Single(g => g.Name == generatorName).Create(new Random(seed));
        // The generator fills the board on the first click, as in the game; read the layout back from it.
        var matrix = new FieldMatrix(rows, columns, bombs, generator);
        matrix.Initialize(FirstClick(rows, columns));
        var layout = new bool[rows, columns];
        foreach (Field f in matrix.Fields)
        {
            layout[matrix.LocalPosition(f).X, matrix.LocalPosition(f).Y] = f.IsBomb;
        }
        return layout;
    }

    // Generates boards one by one with each generator, times each, and checks that every board is valid.
    public static void Run(string board, int count, TimeSpan timeLimit)
    {
        foreach (var (name, _) in Generators)
        {
            Run(name, board, count, timeLimit);
        }
    }

    // Generates `count` boards with one generator and prints the timings and the number of invalid boards.
    // Exits the program if a single board takes longer than `timeLimit`.
    private static void Run(string generatorName, string board, int count, TimeSpan timeLimit)
    {
        var (rows, columns, bombCount) = Boards.Parse(board);
        Point<int> first = FirstClick(rows, columns);
        var times = new System.Collections.Generic.List<double>();
        int wrongCount = 0, startNotFree = 0, notSolvable = 0;

        for (int seed = 0; seed < count; ++seed)
        {
            var watch = Stopwatch.StartNew();
            Task<bool[,]> task = Task.Run(() => Generate(generatorName, rows, columns, bombCount, seed));
            if (!task.Wait(timeLimit))
            {
                Console.WriteLine(
                    $"  {generatorName} {board} seed {seed}: no board after {timeLimit.TotalSeconds:F0} s, stopping");
                Environment.Exit(1);
            }
            times.Add(watch.Elapsed.TotalMilliseconds);
            bool[,] bombs = task.Result;

            if (bombs.Cast<bool>().Count(b => b) != bombCount)
            {
                wrongCount++;
            }
            if (StartHasBomb(bombs, first))
            {
                startNotFree++;
            }
            if (!SolvableFromFirstClick(bombs, first))
            {
                notSolvable++;
            }
        }

        times.Sort();
        Console.WriteLine($"{generatorName} {board}: {count} boards, per board: mean {times.Average():F1} ms, "
            + $"median {times[times.Count / 2]:F1} ms, max {times[^1]:F1} ms | "
            + $"wrong bomb count {wrongCount}, bomb in start area {startNotFree}, "
            + $"not solvable by ConstraintSolver {notSolvable}");
    }

    // Whether the 3x3 area around the first click has a bomb.
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
