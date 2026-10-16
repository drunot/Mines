using System.Collections.Generic;
using System.Linq;
using Mines;
using Mines.Solving;

namespace Mines.Tests;

public class SolverTests
{
    // Seeded random boards: (rows, columns, bombs, seed). Same boards on every run.
    public static IEnumerable<object[]> RandomBoards()
    {
        for (int seed = 0; seed < 200; ++seed)
        {
            yield return new object[] { 9, 16, 20, seed };
        }
        for (int seed = 0; seed < 50; ++seed)
        {
            yield return new object[] { 16, 30, 99, seed };
        }
    }

    [Theory]
    [InlineData(nameof(NeighborSolver))]
    [InlineData(nameof(ConstraintSolver))]
    public void Flags_the_only_bomb(string solver_name)
    {
        FieldMatrix matrix = Boards.FromText(
            "*..",
            "...",
            "...");
        matrix[2, 2].Hidden = false;

        Boards.Solver(solver_name).Solve(matrix);

        Assert.True(matrix[0, 0].Flagged);
        Assert.True(Boards.IsSolved(matrix));
    }

    // A solver must never be wrong: no flag on a safe cell, no bomb revealed.
    [Theory]
    [InlineData(nameof(NeighborSolver))]
    [InlineData(nameof(ConstraintSolver))]
    public void Never_makes_a_wrong_move(string solver_name)
    {
        foreach (object[] board in RandomBoards())
        {
            var (rows, columns, bombs, seed) = ((int)board[0], (int)board[1], (int)board[2], (int)board[3]);
            FieldMatrix matrix = Boards.Random(rows, columns, bombs, seed);
            Field? start = Boards.ZeroCell(matrix);
            if (start is null)
            {
                continue;
            }
            start.Hidden = false;

            Boards.Solver(solver_name).Solve(matrix);

            foreach (Field f in Boards.All(matrix))
            {
                string where = $"{rows}x{columns}/{bombs} seed {seed}, cell ({f.Position.X},{f.Position.Y})";
                Assert.False(f.Flagged && false == f.IsBomb, $"Flagged a safe cell: {where}");
                Assert.False(f.IsBomb && false == f.Hidden, $"Revealed a bomb: {where}");
            }
        }
    }

    // The constraint solvers must find at least everything NeighborSolver finds.
    [Theory]
    [InlineData(nameof(ConstraintSolver))]
    public void Finds_at_least_what_NeighborSolver_finds(string solver_name)
    {
        foreach (object[] board in RandomBoards())
        {
            var (rows, columns, bombs, seed) = ((int)board[0], (int)board[1], (int)board[2], (int)board[3]);
            FieldMatrix reference = Boards.Random(rows, columns, bombs, seed);
            Field? start = Boards.ZeroCell(reference);
            if (start is null)
            {
                continue;
            }
            FieldMatrix tested = new FieldMatrix(reference);
            reference.FieldAt(start.Index).Hidden = false;
            tested.FieldAt(start.Index).Hidden = false;

            new NeighborSolver().Solve(reference);
            Boards.Solver(solver_name).Solve(tested);

            foreach (Field expected in Boards.All(reference))
            {
                Field actual = tested.FieldAt(expected.Index);
                string where = $"{rows}x{columns}/{bombs} seed {seed}, cell ({expected.Position.X},{expected.Position.Y})";
                Assert.False(false == expected.Hidden && actual.Hidden, $"Missed a reveal: {where}");
                Assert.False(expected.Flagged && false == actual.Flagged, $"Missed a flag: {where}");
            }
        }
    }

    // After clicking (2,0) the top row stays hidden with the numbers 1 1 1 below it.
    // No single number decides anything, but comparing them does: the middle 1 covers all
    // three cells and the outer 1s cover two each, so (0,0) and (0,2) are safe and (0,1) is the bomb.
    private static FieldMatrix SubsetBoard()
    {
        FieldMatrix matrix = Boards.FromText(
            ".*.",
            "...",
            "...");
        matrix[2, 0].Hidden = false;
        return matrix;
    }

    [Fact]
    public void NeighborSolver_cannot_solve_the_subset_board()
    {
        FieldMatrix matrix = SubsetBoard();

        new NeighborSolver().Solve(matrix);

        Assert.False(Boards.IsSolved(matrix));
    }

    // ConstraintSolver beats NeighborSolver here by comparing equations (subset step).
    [Theory]
    [InlineData(nameof(ConstraintSolver))]
    public void Solves_the_subset_board(string solver_name)
    {
        FieldMatrix matrix = SubsetBoard();

        Boards.Solver(solver_name).Solve(matrix);

        Assert.True(matrix[0, 1].Flagged);
        Assert.True(Boards.IsSolved(matrix));
    }
}
