using System.Linq;
using Mines;
using Mines.Solving;

namespace Mines.Tests;

// FieldsLeft and BombsLeft are kept up to date by the fields as they change. These tests compare
// them with a direct count of the cells, so bookkeeping bugs show up even when no deduction breaks.
public class FieldMatrixTests
{
    // Hidden cells without a flag.
    private static int CountUnknown(FieldMatrix matrix) => Boards.All(matrix).Count(f => f.Hidden && !f.Flagged);

    // Bombs minus flags placed, minus bombs revealed (only when the game is lost).
    private static int CountBombsLeft(FieldMatrix matrix)
    {
        var fields = Boards.All(matrix).ToList();
        return fields.Count(f => f.IsBomb) - fields.Count(f => f.Flagged) - fields.Count(f => f.IsBomb && !f.Hidden);
    }

    private static void AssertCountersMatch(FieldMatrix matrix, string where)
    {
        Assert.True(CountUnknown(matrix) == matrix.FieldsLeft,
            $"FieldsLeft is {matrix.FieldsLeft}, counted {CountUnknown(matrix)} ({where})");
        Assert.True(CountBombsLeft(matrix) == matrix.BombsLeft,
            $"BombsLeft is {matrix.BombsLeft}, counted {CountBombsLeft(matrix)} ({where})");
    }

    private static FieldMatrix SmallBoard() => Boards.FromText(
        "*....",
        ".....",
        ".....",
        "....*");

    [Fact]
    public void Counters_start_at_the_board_totals()
    {
        FieldMatrix matrix = SmallBoard();

        Assert.Equal(20, matrix.FieldsLeft);
        Assert.Equal(2, matrix.BombsLeft);
    }

    [Fact]
    public void Counters_follow_a_cascade()
    {
        FieldMatrix matrix = SmallBoard();

        matrix[2, 1].Hidden = false;   // no neighbouring bombs: reveals a whole area

        Assert.True(CountUnknown(matrix) < 19, "the reveal should have cascaded");
        AssertCountersMatch(matrix, "after a cascade");
    }

    [Fact]
    public void Counters_follow_flagging_and_unflagging()
    {
        FieldMatrix matrix = SmallBoard();

        matrix[0, 0].Flagged = true;
        matrix[3, 4].Flagged = true;
        AssertCountersMatch(matrix, "after two flags");

        matrix[3, 4].Flagged = false;
        AssertCountersMatch(matrix, "after removing one flag");
    }

    [Fact]
    public void Revealing_a_flagged_cell_only_removes_the_flag()
    {
        FieldMatrix matrix = SmallBoard();
        matrix[0, 0].Flagged = true;

        matrix[0, 0].Hidden = false;

        Assert.True(matrix[0, 0].Hidden);
        Assert.False(matrix[0, 0].Flagged);
        AssertCountersMatch(matrix, "after revealing a flagged cell");
    }

    // A full solve reveals and flags many cells; the copy constructor's counters are checked too.
    [Fact]
    public void Counters_match_after_solving()
    {
        for (int seed = 0; seed < 50; ++seed)
        {
            FieldMatrix layout = Boards.Random(16, 30, 99, seed);
            Field? start = Boards.ZeroCell(layout);
            if (start is null)
            {
                continue;
            }
            var matrix = new FieldMatrix(layout);
            matrix[start.Position.X, start.Position.Y].Hidden = false;

            new ConstraintSolver().Solve(matrix);

            AssertCountersMatch(matrix, $"16x30/99 seed {seed} after solving");
        }
    }
}
