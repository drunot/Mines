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

    // Bombs minus flags placed. Revealed bombs (a lost game) don't count.
    private static int CountBombsLeft(FieldMatrix matrix)
    {
        var fields = Boards.All(matrix).ToList();
        return fields.Count(f => f.IsBomb) - fields.Count(f => f.Flagged);
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

    [Fact]
    public void Counters_follow_hiding_a_field_again()
    {
        FieldMatrix matrix = SmallBoard();
        matrix[0, 1].Hidden = false;

        matrix[0, 1].Hidden = true;

        Assert.Equal(20, matrix.FieldsLeft);
        AssertCountersMatch(matrix, "after hiding a field again");
    }

    [Fact]
    public void Losing_does_not_change_the_bomb_counter()
    {
        FieldMatrix matrix = SmallBoard();
        matrix[0, 0].Flagged = true;

        matrix[3, 4].Hidden = false;   // a bomb: the game is lost

        Assert.True(matrix.GameLost);
        Assert.Equal(1, matrix.BombsLeft);
        AssertCountersMatch(matrix, "after losing");
    }

    [Fact]
    public void Classic_generator_keeps_the_first_click_free()
    {
        // 9 cells and 8 bombs: the only free cell must be the clicked one.
        for (int seed = 0; seed < 20; ++seed)
        {
            var generator = new Mines.Generators.ClassicRandomGenerator(new System.Random(seed));
            bool[,] bombs = Boards.Generate(generator, 3, 3, new Point<int>(1, 2), 8);
            Assert.False(bombs[1, 2], $"seed {seed} put a bomb under the first click");
        }
    }

    [Fact]
    public void Marks_cycle_and_only_flags_count()
    {
        FieldMatrix matrix = SmallBoard();

        matrix.CycleMark(0, 0);   // flag
        Assert.Equal(Mark.Flag, matrix[0, 0].Mark);
        AssertCountersMatch(matrix, "after flagging");
        Assert.Equal(1, matrix.BombsLeft);

        matrix.CycleMark(0, 0);   // question mark: the flag no longer counts
        Assert.True(matrix[0, 0].Questioned);
        Assert.False(matrix[0, 0].Flagged);
        AssertCountersMatch(matrix, "after the question mark");
        Assert.Equal(2, matrix.BombsLeft);

        matrix.CycleMark(0, 0);   // none
        Assert.Equal(Mark.None, matrix[0, 0].Mark);
        AssertCountersMatch(matrix, "after removing the mark");
    }

    [Fact]
    public void Revealing_a_question_marked_cell_removes_the_mark()
    {
        FieldMatrix matrix = SmallBoard();
        matrix[1, 1].Questioned = true;

        matrix[1, 1].Hidden = false;

        Assert.False(matrix[1, 1].Hidden);
        Assert.Equal(Mark.None, matrix[1, 1].Mark);
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
            matrix.FieldAt(start.Index).Hidden = false;

            new ConstraintSolver().Solve(matrix);

            AssertCountersMatch(matrix, $"16x30/99 seed {seed} after solving");
        }
    }
}
