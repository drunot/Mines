using System;
using System.Linq;
using Mines.BoardFixer;
using Mines.Solving;

namespace Mines.Tests;

// EdgeBoardFixer with a lock mask: what the player of an infinite board has seen must never change.
public class BoardFixerTests
{
    // Random boards, solved as far as possible, with a random third of the cells locked: no swap may touch a
    // locked cell, at either end.
    [Fact]
    public void Swaps_never_touch_locked_cells()
    {
        int fixes = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            FieldMatrix layout = Boards.Random(16, 30, 99, seed);
            Field? start = Boards.ZeroCell(layout);
            if (start is null)
            {
                continue;
            }
            var board = new FieldMatrix(layout);
            board.FieldAt(start.Index).Hidden = false;
            new ConstraintSolver().Solve(board);

            var random = new Random(seed);
            var locked = new bool[board.Rows, board.Columns];
            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    locked[r, c] = random.Next(3) == 0;
                }
            }

            var fixer = new EdgeBoardFixer(new Random(seed));
            if (!fixer.GetBoardChanges(board, out var swaps, locked))
            {
                continue;
            }
            fixes++;
            foreach (var (a, b) in swaps)
            {
                Assert.False(locked[a.X, a.Y], $"seed {seed}: swap touches locked cell ({a.X},{a.Y})");
                Assert.False(locked[b.X, b.Y], $"seed {seed}: swap touches locked cell ({b.X},{b.Y})");
            }
        }
        Assert.True(fixes > 0, "no board needed a fix, so nothing was checked");
    }
}
