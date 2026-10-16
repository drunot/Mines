// Solver and generator benchmarks. Run from this folder:
//   dotnet run -c Release -- --filter '*'                  all benchmarks
//   dotnet run -c Release -- --filter '*SolverBench*'      only the solvers
//   dotnet run -c Release -- --filter '*GeneratorBench*'   only board generation (see GeneratorBench.cs)
//   dotnet run -c Release -- check                         compare every solver with NeighborSolver
//   dotnet run -c Release -- gencheck [factor]             time board generation and validate every board
//                                                          (factor multiplies the number of boards, default 1)
//
// Boards are generated from fixed seeds, so every run and every benchmark solves the same
// boards and results stay comparable while optimizing. Correctness is covered by ../Tests.
//
// To add or drop a solver: add or remove its [Benchmark] method and its entry in Solvers.

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Mines;
using Mines.Solving;

// Time to solve a fixed set of boards from their start cell, per solver and board size.
[MemoryDiagnoser]
public class SolverBench
{
    [Params("9x16/20", "16x30/99", "100x100/2000")]
    public string Board = "";

    private List<StartBoard> _boards = new();

    [GlobalSetup]
    public void Setup()
    {
        var (rows, columns, bombs) = Boards.Parse(Board);
        // Fewer large boards, so one run doesn't take minutes.
        int count = rows * columns > 1000 ? 20 : 200;
        _boards = Boards.Make(rows, columns, bombs, count);
    }

    // Copying the board and the first reveal only. Subtract this from the others.
    [Benchmark(Baseline = true)]
    public int SetupOnly() => Run(null);

    [Benchmark]
    public int NeighborSolver() => Run(new NeighborSolver());

    [Benchmark]
    public int ConstraintSolver() => Run(new ConstraintSolver());

    private int Run(ISolver? solver)
    {
        int result = 0;
        foreach (StartBoard board in _boards)
        {
            FieldMatrix matrix = board.Start();
            solver?.Solve(matrix);
            result += matrix.Rows;
        }
        return result;
    }
}

// A board plus a start cell with no neighbouring bombs.
public record StartBoard(FieldMatrix Template, int Row, int Column)
{
    // A fresh, unsolved copy with the start cell revealed.
    public FieldMatrix Start()
    {
        var matrix = new FieldMatrix(Template);
        matrix[Row, Column].Hidden = false;
        return matrix;
    }
}

// Seeded random boards shared by the solver benchmarks and the check command.
public static class Boards
{
    // "rows x columns / bombs", e.g. "16x30/99".
    public static (int Rows, int Columns, int Bombs) Parse(string board)
    {
        int[] parts = board.Split('x', '/').Select(int.Parse).ToArray();
        return (parts[0], parts[1], parts[2]);
    }

    // `count` boards that each have a start cell, the same ones on every run.
    public static List<StartBoard> Make(int rows, int columns, int bombCount, int count)
    {
        var boards = new List<StartBoard>();
        for (int seed = 0; boards.Count < count; ++seed)
        {
            var matrix = new FieldMatrix(RandomBombs(rows, columns, bombCount, seed));
            Field? start = matrix.Fields.Cast<Field>().FirstOrDefault(f => !f.IsBomb && f.Number is null);
            if (start is not null)
            {
                Point<int> local = matrix.LocalPosition(start);
                boards.Add(new StartBoard(matrix, local.X, local.Y));
            }
        }
        return boards;
    }

    // Random layout that is the same for the same seed: bombs[row, column] is true for a bomb.
    private static bool[,] RandomBombs(int rows, int columns, int bombCount, int seed)
    {
        var rand = new Random(seed);
        var bombs = new bool[rows, columns];
        int placed = 0;
        while (placed < bombCount)
        {
            int r = rand.Next(rows);
            int c = rand.Next(columns);
            if (false == bombs[r, c])
            {
                bombs[r, c] = true;
                ++placed;
            }
        }
        return bombs;
    }
}

// Runs the benchmarks, or the check / gencheck commands described at the top of this file.
public static class Program
{
    // The solvers that the check command compares with NeighborSolver.
    private static readonly (string Name, Func<ISolver> Create)[] Solvers =
    {
        (nameof(ConstraintSolver), () => new ConstraintSolver()),
    };

    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "check")
        {
            Check("9x16/20", 2000);
            Check("16x30/99", 2000);
            Check("100x100/2000", 200);
            return;
        }
        if (args.Length > 0 && args[0] == "gencheck")
        {
            // Optional multiplier for the number of boards, e.g. "gencheck 20" for a longer soak test.
            int factor = args.Length > 1 && int.TryParse(args[1], out int f) && f > 0 ? f : 1;
            GeneratorCheck.Run("9x16/20", 200 * factor, TimeSpan.FromSeconds(30));
            GeneratorCheck.Run("16x30/99", 100 * factor, TimeSpan.FromSeconds(30));
            GeneratorCheck.Run("100x100/2000", 10 * factor, TimeSpan.FromSeconds(120));
            return;
        }
        BenchmarkSwitcher.FromTypes(new[] { typeof(SolverBench), typeof(GeneratorBench) }).Run(args);
    }

    // How each solver compares with NeighborSolver: same result, more found, wrong moves.
    private static void Check(string board, int count)
    {
        var (rows, columns, bombs) = Boards.Parse(board);
        List<StartBoard> boards = Boards.Make(rows, columns, bombs, count);

        var reference = boards
            .Select(b => { FieldMatrix m = b.Start(); new NeighborSolver().Solve(m); return m; })
            .ToList();
        Console.WriteLine($"{board}: {count} boards, NeighborSolver fully solves {reference.Count(IsSolved)}");

        foreach (var (name, create) in Solvers)
        {
            int same = 0, solved = 0, wrongMoves = 0, cellsBeyond = 0;
            for (int i = 0; i < boards.Count; ++i)
            {
                FieldMatrix m = boards[i].Start();
                create().Solve(m);

                solved += IsSolved(m) ? 1 : 0;
                wrongMoves += All(m).Count(f => (f.Flagged && !f.IsBomb) || (f.IsBomb && !f.Hidden));
                int known = All(m).Count(f => !f.Hidden || f.Flagged);
                int knownReference = All(reference[i]).Count(f => !f.Hidden || f.Flagged);
                cellsBeyond += known - knownReference;
                same += known == knownReference ? 1 : 0;
            }
            Console.WriteLine($"  {name}: fully solves {solved}, same as NeighborSolver on {same}/{count}, "
                + $"avg extra cells decided {cellsBeyond / (double)count:F1}, wrong moves {wrongMoves}");
        }
    }

    // Every field of the board, row by row.
    private static IEnumerable<Field> All(FieldMatrix m) => m.Fields.Cast<Field>();

    // True when every safe cell is revealed.
    private static bool IsSolved(FieldMatrix m) => All(m).All(f => f.IsBomb || !f.Hidden);
}
