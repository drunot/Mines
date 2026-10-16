using System;
using System.Collections.Generic;
using System.Linq;
using Mines;
using Mines.Solving;

namespace Mines.Tests;

// The endless board: chunks around revealed cells, numbers across chunk borders, the cascade limited to the
// visible area, and the streak counter.
public class InfiniteFieldMatrixTests
{
    // All fields that exist in the square of cells from -size to size in both directions.
    private static List<Field> FieldsAround(InfiniteFieldMatrix board, long size)
    {
        var fields = new List<Field>();
        for (long r = -size; r <= size; r++)
        {
            for (long c = -size; c <= size; c++)
            {
                if (board.FieldAt(r, c) is Field f)
                {
                    fields.Add(f);
                }
            }
        }
        return fields;
    }

    [Fact]
    public void First_click_keeps_the_start_area_free_and_reveals_the_cell()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var board = new InfiniteFieldMatrix(0.3f, GeneratorType.Deterministic, new Random(seed));
            board.SetVisibleArea(-10, -10, 10, 10);

            board.Reveal(7, -1);   // near a chunk border, so the start area spans two chunks

            Assert.False(board.FieldAt(7, -1)!.Hidden);
            for (long r = 6; r <= 8; r++)
            {
                for (long c = -2; c <= 0; c++)
                {
                    Assert.False(board.FieldAt(r, c)!.IsBomb, $"seed {seed}: bomb at ({r},{c}) in the start area");
                }
            }
            Assert.Equal(0, board.BombsHit);
        }
    }

    [Fact]
    public void Revealed_numbers_count_all_neighbours_also_across_chunk_borders()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var board = new InfiniteFieldMatrix(0.12f, GeneratorType.Deterministic, new Random(seed));
            board.SetVisibleArea(-20, -20, 20, 20);
            board.Reveal(0, 0);

            foreach (Field f in FieldsAround(board, 60).Where(f => !f.Hidden && !f.IsBomb))
            {
                // Every neighbour of a revealed cell must exist, so its number is complete.
                int bombs = 0;
                for (long r = f.Position.X - 1; r <= f.Position.X + 1; r++)
                {
                    for (long c = f.Position.Y - 1; c <= f.Position.Y + 1; c++)
                    {
                        Field? n = board.FieldAt(r, c);
                        Assert.True(n is not null, $"seed {seed}: neighbour ({r},{c}) of a revealed cell is missing");
                        bombs += (r, c) != (f.Position.X, f.Position.Y) && n!.IsBomb ? 1 : 0;
                    }
                }
                Assert.Equal(bombs, f.Number ?? 0);
                Assert.Equal(8, f.Neighbors.Length);
            }
        }
    }

    [Fact]
    public void Cascade_stays_in_the_active_area_and_continues_when_the_view_moves()
    {
        var board = new InfiniteFieldMatrix(0f, GeneratorType.Deterministic, new Random(1));   // no bombs at all
        board.SetVisibleArea(0, 0, 14, 14);   // chunk (0, 0); active: chunks -1..1, cells -15..29

        board.Reveal(5, 5);

        List<Field> revealed = FieldsAround(board, 200).Where(f => !f.Hidden).ToList();
        Assert.Equal(45 * 45, revealed.Count);
        Assert.All(revealed, f => Assert.InRange(f.Position.X, -15, 29));
        Assert.All(revealed, f => Assert.InRange(f.Position.Y, -15, 29));
        Assert.Equal(1, board.Streak);

        board.SetVisibleArea(0, 15, 14, 29);   // one chunk to the right: one more column of chunks becomes active

        Assert.Equal(45 * 60, FieldsAround(board, 200).Count(f => !f.Hidden));
        Assert.Equal(1, board.Streak);   // moving the view isn't a click
    }

    [Fact]
    public void Streak_counts_safe_clicks_and_a_bomb_resets_it()
    {
        var board = new InfiniteFieldMatrix(0.2f, GeneratorType.Deterministic, new Random(3));
        board.SetVisibleArea(-20, -20, 20, 20);

        board.Reveal(0, 0);
        Assert.Equal(1, board.Streak);

        board.Reveal(0, 0);   // already revealed (and not a chord): nothing happens
        Assert.Equal(1, board.Streak);

        Field safe = FieldsAround(board, 14).First(f => f.Hidden && !f.IsBomb);
        board.Reveal(safe.Position.X, safe.Position.Y);
        Assert.Equal(2, board.Streak);
        Assert.False(board.LastActionHitBomb);

        Field bomb = FieldsAround(board, 14).First(f => f.Hidden && f.IsBomb);
        board.Reveal(bomb.Position.X, bomb.Position.Y);
        Assert.Equal(0, board.Streak);
        Assert.Equal(2, board.BestStreak);
        Assert.Equal(1, board.BombsHit);
        Assert.True(board.LastActionHitBomb);
        Assert.False(board.GameLost);   // the game goes on
        Assert.False(bomb.Hidden);
    }

    // The no-guess promise: after the first click, a player who only uses logic can finish the whole first chunk.
    // Checked by solving the window around it from what the player sees, with what the player knows (no bomb
    // total, no numbers from the window's edge).
    [Theory]
    [InlineData(0.10f)]
    [InlineData(0.15f)]
    [InlineData(0.20f)]
    public void First_chunk_can_be_solved_by_logic(float density)
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var board = new InfiniteFieldMatrix(density, GeneratorType.Deterministic, new Random(seed));
            board.SetVisibleArea(-30, -30, 30, 30);
            board.Reveal(7, 7);   // the middle of chunk (0, 0)

            FieldMatrix window = board.CreateWindow(0, 0, out _);
            new ConstraintSolver { UseGlobalEquation = false, IgnoreEdges = true }.Solve(window);

            for (int r = 15; r < 30; r++)
            {
                for (int c = 15; c < 30; c++)
                {
                    Assert.False(window[r, c].Hidden && !window[r, c].IsBomb,
                        $"density {density}, seed {seed}: cell ({r - 15},{c - 15}) of the first chunk needs a guess");
                }
            }
        }
    }

    // A chord is worth the clicks it saves, so chording never costs streak.
    [Fact]
    public void Chord_counts_one_click_per_neighbour_it_opens()
    {
        int checkedBoards = 0;
        for (int seed = 0; seed < 50 && checkedBoards < 5; seed++)
        {
            var board = new InfiniteFieldMatrix(0.2f, GeneratorType.Deterministic, new Random(seed));
            board.SetVisibleArea(-20, -20, 20, 20);
            board.Reveal(0, 0);

            // A revealed number with at least two hidden safe neighbours that are numbers themselves (no cascade).
            Field? number = FieldsAround(board, 14).FirstOrDefault(f =>
                !f.Hidden && f.Number is not null
                && f.Neighbors.Count(n => n.Hidden && !n.IsBomb) >= 2
                && f.Neighbors.Where(n => n.Hidden && !n.IsBomb).All(n => n.Number is not null));
            if (number is null)
            {
                continue;
            }
            foreach (Field bomb in number.Neighbors.Where(n => n.Hidden && n.IsBomb))
            {
                board.CycleMark(bomb.Position.X, bomb.Position.Y);   // flag every bomb around it
            }
            int opens = number.Neighbors.Count(n => n.Hidden && !n.Flagged);
            int before = board.Streak;

            board.Reveal(number.Position.X, number.Position.Y);   // chord

            Assert.Equal(before + opens, board.Streak);
            Assert.Equal(board.Streak, board.BestStreak);
            checkedBoards++;
        }
        Assert.True(checkedBoards > 0, "no board with a suitable number was found");
    }

    [Fact]
    public void Saved_board_continues_where_it_was()
    {
        var board = new InfiniteFieldMatrix(0.05f, GeneratorType.Deterministic, new Random(5));
        board.SetVisibleArea(0, 0, 14, 14);   // small active area: the cascade leaves cells pending
        board.Reveal(5, 5);
        Field hidden = FieldsAround(board, 14).First(f => f.Hidden);
        board.CycleMark(hidden.Position.X, hidden.Position.Y);   // a flag
        Field bomb = FieldsAround(board, 40).First(f => f.Hidden && f.IsBomb && !f.Flagged);
        board.Reveal(bomb.Position.X, bomb.Position.Y);          // a hit bomb stays revealed

        InfiniteFieldMatrix? loaded = InfiniteFieldMatrix.Load(board.Save(), 0.05f, GeneratorType.Deterministic);

        Assert.NotNull(loaded);
        foreach (Field f in FieldsAround(board, 60))
        {
            Field? g = loaded!.FieldAt(f.Position.X, f.Position.Y);
            Assert.NotNull(g);
            Assert.Equal((f.IsBomb, f.Hidden, f.Mark), (g!.IsBomb, g.Hidden, g.Mark));
        }
        Assert.Equal((board.Streak, board.BestStreak, board.BombsHit, board.LastActionHitBomb),
                     (loaded!.Streak, loaded.BestStreak, loaded.BombsHit, loaded.LastActionHitBomb));
        Assert.True(loaded.Initialized);

        // The cascade that waited at the edge of the active area continues: once the area moves, no revealed 0
        // inside it has a hidden, unflagged neighbour inside it. (New chunks get new random bombs, so the two
        // boards can differ from here on.)
        loaded.SetVisibleArea(0, 15, 14, 29);   // active: chunks -1..1 by 0..2, cells -15..29 by 0..44
        static bool Active(Field f) => f.Position.X >= -15 && f.Position.X <= 29 && f.Position.Y >= 0 && f.Position.Y <= 44;
        foreach (Field f in FieldsAround(loaded, 60).Where(f => Active(f) && !f.Hidden && !f.IsBomb && f.Number is null))
        {
            Assert.DoesNotContain(f.Neighbors, n => Active(n) && n.Hidden && !n.Flagged);
        }
    }

    [Fact]
    public void Damaged_save_is_refused()
    {
        var state = new InfiniteBoardState();
        state.Chunks.Add(new ChunkState { Row = 0, Column = 0, Cells = "abc" });

        Assert.Null(InfiniteFieldMatrix.Load(state, 0.1f, GeneratorType.Random));
    }

    [Fact]
    public void Marks_cycle_flag_question_none()
    {
        var board = new InfiniteFieldMatrix(0.2f, GeneratorType.Deterministic, new Random(4));
        board.SetVisibleArea(-20, -20, 20, 20);
        board.Reveal(0, 0);
        Field cell = FieldsAround(board, 14).First(f => f.Hidden);
        (long r, long c) = (cell.Position.X, cell.Position.Y);

        board.CycleMark(r, c);
        Assert.True(cell.Flagged);
        board.CycleMark(r, c);
        Assert.False(cell.Flagged);
        Assert.True(cell.Questioned);
        board.CycleMark(r, c);
        Assert.False(cell.Flagged);
        Assert.False(cell.Questioned);
    }
}
