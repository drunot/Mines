using System;
using System.Collections.Generic;
using System.Linq;
using Mines;
using Mines.Generators;
using Mines.Solving;

namespace Mines.Tests;

// Helpers for building boards and inspecting them after a solver has run.
internal static class Boards
{
    // Board drawn as text, one string per row: '*' is a bomb, anything else is safe.
    public static FieldMatrix FromText(params string[] rows)
    {
        var bombs = new bool[rows.Length, rows[0].Length];
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length; c++)
            {
                bombs[r, c] = rows[r][c] == '*';
            }
        }
        return new FieldMatrix(bombs);
    }

    // Random board that is the same for the same seed, so a failing test can be rerun.
    public static FieldMatrix Random(int rows, int columns, int bomb_count, int seed)
    {
        var rand = new Random(seed);
        var bombs = new bool[rows, columns];
        int placed = 0;
        while (placed < bomb_count)
        {
            int r = rand.Next(rows);
            int c = rand.Next(columns);
            if (false == bombs[r, c])
            {
                bombs[r, c] = true;
                ++placed;
            }
        }
        return new FieldMatrix(bombs);
    }

    // First safe cell with no neighbouring bombs, or null if the board has none.
    public static Field? ZeroCell(FieldMatrix matrix)
    {
        return matrix.Fields.Cast<Field>().FirstOrDefault(f => false == f.IsBomb && f.Number is null);
    }

    // Solvers by class name, so tests can list them in [InlineData].
    public static ISolver Solver(string name) => name switch
    {
        nameof(NeighborSolver) => new NeighborSolver(),
        nameof(ConstraintSolver) => new ConstraintSolver(),
        _ => throw new ArgumentException($"Unknown solver {name}", nameof(name)),
    };

    // Lets the generator place the bombs, the way the game does on the first click, and returns the layout:
    // bombs[row, column] is true for a bomb.
    public static bool[,] Generate(IMatrixGenerator generator, int rows, int columns, Point<int> first, int bomb_count)
    {
        var matrix = new FieldMatrix(rows, columns, bomb_count, generator);
        matrix.Initialize(first);
        var bombs = new bool[rows, columns];
        foreach (Field f in matrix.Fields)
        {
            bombs[matrix.LocalPosition(f).X, matrix.LocalPosition(f).Y] = f.IsBomb;
        }
        return bombs;
    }

    public static IEnumerable<Field> All(FieldMatrix matrix) => matrix.Fields.Cast<Field>();

    public static bool IsSolved(FieldMatrix matrix) => All(matrix).All(f => f.IsBomb || false == f.Hidden);
}
