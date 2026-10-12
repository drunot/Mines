using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Mines.BoardFixer;

/// <summary>
/// Fixes a stuck board at the edge of the revealed area: picks the revealed number that is cheapest to
/// make decidable and either fills all its unknown neighbours with bombs, or moves all its remaining
/// bombs away into the untouched part of the board.
/// </summary>
public class EdgeBoardFixer : iBoardFixer
{

    /// <summary>
    /// A revealed numbered field considered for fixing, with a score of how many bombs need moving.
    /// </summary>
    /// <remarks>
    /// The score is even (2 * bombs missing) for a "take" fix and odd (2 * bombs in excess - 1) for a
    /// "give" fix, so one number encodes both the cost and the kind of fix. Lower is better;
    /// <see cref="ErrorScore"/> means the field can't be used.
    /// </remarks>
    class FieldCandidate
    {
        /// <summary>The score of a field that can't be used for fixing; sorts last.</summary>
        public const int ErrorScore = 99999;

        /// <summary>
        /// Creates a candidate for <paramref name="field"/>.
        /// </summary>
        /// <param name="field">The revealed numbered field.</param>
        public FieldCandidate(Field field)
        {
            _field = field;
        }

        private Field _field;

        /// <summary>The candidate field.</summary>
        public Field Field
        {
            get => _field;
        }

        /// <summary>Cached <see cref="SelectionScore"/>; <c>null</c> until computed.</summary>
        private int? _score = null;

        /// <summary>
        /// Computes the score: the cheaper of filling all unknown neighbours with bombs ("take", even score)
        /// or moving the remaining bombs away ("give", odd score).
        /// </summary>
        /// <returns>The score, or <see cref="ErrorScore"/> if the field can't be used.</returns>
        private int GenerateSelectionScore()
        {
            if (_field.Number is null || _field.Hidden)
            {
                return ErrorScore; // Field not valid for fixing. Put it last.
            }
            int unknown = _field.Neighbors.Count((Field f) => f.Hidden && !f.Flagged);
            int remaining = (_field.Number ?? 0) - _field.Neighbors.Count((Field f) => f.Flagged);

            int take_score = (unknown - remaining) * 2;   // even: cells to fill with bombs
            int give_score = remaining * 2 - 1;           // odd: bombs to move away

            if (take_score <= 0 || give_score < 0)
            {
                return ErrorScore; // Field not valid for fixing. Put it last.
            }

            // Set score to the smallest score.
            return take_score > give_score ? give_score : take_score;
        }


        /// <summary>
        /// The selection score, computed on first use. Lower is better; see the remarks on
        /// <see cref="FieldCandidate"/>.
        /// </summary>
        public int SelectionScore
        {
            get
            {
                if (_score is null)
                {
                    _score = GenerateSelectionScore();
                }
                return (int)_score;

            }
        }

        /// <summary>Whether the fix is to add bombs to the unknown neighbours (even score).</summary>
        public bool ShouldTake
        {
            get => SelectionScore % 2 == 0;
        }

        /// <summary>Whether the fix is to move bombs away from the neighbours (odd score, not an error).</summary>
        public bool ShouldGive
        {
            get => SelectionScore != ErrorScore && SelectionScore % 2 == 1;
        }

        /// <summary>The number of bombs to add around the field; 0 for a "give" fix.</summary>
        public int BombsMissing
        {
            get
            {
                if (ShouldGive)
                {
                    return 0;
                }
                return SelectionScore / 2;
            }
        }

        /// <summary>The number of bombs to move away from around the field; 0 for a "take" fix.</summary>
        public int BombsInExcess
        {
            get
            {
                if (ShouldTake)
                {
                    return 0;
                }
                return (SelectionScore + 1) / 2;
            }
        }
    }

    /// <summary>
    /// Creates the fixer.
    /// </summary>
    /// <param name="random">The random number source used to pick where bombs go; a new <see cref="Random"/> if <c>null</c>.</param>
    public EdgeBoardFixer(Random? random = null)
    {
        _rand = random ?? new Random();
    }

    /// <summary>The random number source.</summary>
    private Random _rand;

    /// <inheritdoc/>
    /// <remarks>
    /// Swaps bombs with hidden fields whose neighbours are all hidden, so no revealed number away from the
    /// chosen edge changes. Returns <c>false</c> if no revealed number can be fixed, or if there aren't enough
    /// such hidden fields.
    /// </remarks>
    public bool GetBoardChanges(FieldMatrix board, [NotNullWhen(true)] out List<(Point<int>, Point<int>)>? swaps)
    {
        List<FieldCandidate> candidates = new List<FieldCandidate>();
        foreach (Field f in board.Fields)
        {
            if (f.Number is not null && !f.Hidden)
            {
                candidates.Add(new FieldCandidate(f));
            }
        }

        // If no candidates was found no fixes can be applied.
        if(candidates.Count == 0)
        {
            swaps = null;
            return false;
        }
        candidates.Sort((f1, f2) => f1.SelectionScore.CompareTo(f2.SelectionScore));
        var best_candidate = candidates.First();

        if (best_candidate.SelectionScore == FieldCandidate.ErrorScore)
        {
            swaps = null;
            return false;
        }


        if (best_candidate.ShouldGive)
        {
            List<Field> available_fields = new List<Field>();
            foreach (Field f in board.Fields)
            {
                if (f.Neighbors.All((fn) => fn.Hidden) && !f.IsBomb && f.Hidden)
                {
                    available_fields.Add(f);
                }
            }
            if (available_fields.Count < best_candidate.BombsInExcess)
            {
                // TODO: Attempt the best take candidate before giving up.
                swaps = null;
                return false;
            }
            swaps = new List<(Point<int>, Point<int>)>();

            Field[] fields_shuffled = available_fields.ToArray();
            _rand.Shuffle(fields_shuffled);
            int i = 0;
            foreach (var fn in best_candidate.Field.Neighbors)
            {
                // Only swap unflagged bombs.
                if (!fn.IsBomb || fn.Flagged)
                {
                    continue;
                }
                swaps.Add((fields_shuffled[i++].Position, fn.Position));
            }
            return true;
        }
        else
        {
            List<Field> available_fields = new List<Field>();
            foreach (Field f in board.Fields)
            {
                if (f.Neighbors.All((fn) => fn.Hidden) && f.IsBomb && !f.Flagged)
                {
                    available_fields.Add(f);
                }
            }
            if (available_fields.Count < best_candidate.BombsMissing)
            {
                // TODO: Attempt the best give candidate before giving up.
                swaps = null;
                return false;
            }
            swaps = new List<(Point<int>, Point<int>)>();

            Field[] fields_shuffled = available_fields.ToArray();
            _rand.Shuffle(fields_shuffled);
            int i = 0;
            foreach (var fn in best_candidate.Field.Neighbors)
            {
                // Only swap hidden fields without bombs.
                if (fn.IsBomb || !fn.Hidden)
                {
                    continue;
                }
                swaps.Add((fields_shuffled[i++].Position, fn.Position));
            }
            return true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Applies the swaps from <see cref="GetBoardChanges"/> to <paramref name="board"/> itself, then returns
    /// its new layout.
    /// </remarks>
    public bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixed_board)
    {
        if (GetBoardChanges(board, out var swaps))
        {
            foreach (var swap in swaps)
            {
                board.SwapBombs(swap.Item1, swap.Item2);
            }

            fixed_board = new bool[board.Rows, board.Columns];
            for (int x = 0; x < board.Rows; ++x)
            {
                for (int y = 0; y < board.Columns; ++y)
                {
                    fixed_board[x, y] = board.Fields[x, y].IsBomb;
                }
            }
            return true;
        }
        fixed_board = null;
        return false;
    }

}