using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Mines.BoardFixer;

/// <summary>
/// Fixes a stuck board at the edge of the revealed area: picks the revealed number that is cheapest to
/// make decidable and either fills all its unknown neighbours with bombs, or moves all its remaining
/// bombs away into the untouched part of the board.
/// </summary>
public class EdgeBoardFixer : IBoardFixer
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
            int unknown = _field.Neighbors.Count(f => f.Hidden && !f.Flagged);
            int remaining = (_field.Number ?? 0) - _field.Neighbors.Count(f => f.Flagged);

            int takeScore = (unknown - remaining) * 2;   // even: cells to fill with bombs
            int giveScore = remaining * 2 - 1;           // odd: bombs to move away

            if (takeScore <= 0 || giveScore < 0)
            {
                return ErrorScore; // Field not valid for fixing. Put it last.
            }

            // Set score to the smallest score.
            return takeScore > giveScore ? giveScore : takeScore;
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
    /// <param name="random">
    /// The random number source used to pick where bombs go; a new <see cref="Random"/> if <c>null</c>.
    /// </param>
    public EdgeBoardFixer(Random? random = null)
    {
        _rand = random ?? new Random();
    }

    /// <summary>The random number source.</summary>
    private Random _rand;

    /// <inheritdoc/>
    /// <remarks>
    /// Swaps bombs with hidden fields whose neighbours are all hidden, so no revealed number away from the
    /// chosen edge changes. No locked field is ever swapped: a revealed number is only used if none of the hidden
    /// fields around it is locked, and locked fields are never used as the far end of a swap. Returns
    /// <c>false</c> if no revealed number can be fixed, or if there aren't enough such hidden fields.
    /// </remarks>
    public bool GetBoardChanges(FieldMatrix board, [NotNullWhen(true)] out List<(Point<long>, Point<long>)>? swaps,
                                bool[,]? locked = null)
    {
        bool IsLocked(Field f)
        {
            if (locked is null)
            {
                return false;
            }
            Point<int> p = board.LocalPosition(f);
            return locked[p.X, p.Y];
        }

        // A number can only be fixed if every hidden field around it may change.
        List<FieldCandidate> candidates = new List<FieldCandidate>();
        foreach (Field f in board.Fields)
        {
            if (f.Number is not null && !f.Hidden && !IsLocked(f)
                && !f.Neighbors.Any(n => n.Hidden && !n.Flagged && IsLocked(n)))
            {
                candidates.Add(new FieldCandidate(f));
            }
        }

        // If no candidates was found no fixes can be applied.
        if (candidates.Count == 0)
        {
            swaps = null;
            return false;
        }
        candidates.Sort((f1, f2) => f1.SelectionScore.CompareTo(f2.SelectionScore));
        var bestCandidate = candidates.First();

        if (bestCandidate.SelectionScore == FieldCandidate.ErrorScore)
        {
            swaps = null;
            return false;
        }

        if (bestCandidate.ShouldGive)
        {
            List<Field> availableFields = new List<Field>();
            foreach (Field f in board.Fields)
            {
                if (f.Neighbors.All(fn => fn.Hidden) && !f.IsBomb && f.Hidden && !IsLocked(f))
                {
                    availableFields.Add(f);
                }
            }
            if (availableFields.Count < bestCandidate.BombsInExcess)
            {
                // TODO: Attempt the best take candidate before giving up.
                swaps = null;
                return false;
            }
            swaps = new List<(Point<long>, Point<long>)>();

            Field[] fieldsShuffled = availableFields.ToArray();
            _rand.Shuffle(fieldsShuffled);
            int i = 0;
            foreach (var fn in bestCandidate.Field.Neighbors)
            {
                // Only swap unflagged bombs.
                if (!fn.IsBomb || fn.Flagged)
                {
                    continue;
                }
                swaps.Add((fieldsShuffled[i++].Position, fn.Position));
            }
            return true;
        }
        else
        {
            List<Field> availableFields = new List<Field>();
            foreach (Field f in board.Fields)
            {
                if (f.Neighbors.All(fn => fn.Hidden) && f.IsBomb && !f.Flagged && !IsLocked(f))
                {
                    availableFields.Add(f);
                }
            }
            if (availableFields.Count < bestCandidate.BombsMissing)
            {
                // TODO: Attempt the best give candidate before giving up.
                swaps = null;
                return false;
            }
            swaps = new List<(Point<long>, Point<long>)>();

            Field[] fieldsShuffled = availableFields.ToArray();
            _rand.Shuffle(fieldsShuffled);
            int i = 0;
            foreach (var fn in bestCandidate.Field.Neighbors)
            {
                // Only swap hidden fields without bombs.
                if (fn.IsBomb || !fn.Hidden)
                {
                    continue;
                }
                swaps.Add((fieldsShuffled[i++].Position, fn.Position));
            }
            return true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Applies the swaps from <see cref="GetBoardChanges"/> to <paramref name="board"/> itself, then returns
    /// its new layout.
    /// </remarks>
    public bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixedBoard)
    {
        if (GetBoardChanges(board, out var swaps))
        {
            foreach (var swap in swaps)
            {
                board.SwapBombs(swap.Item1, swap.Item2);
            }

            fixedBoard = new bool[board.Rows, board.Columns];
            for (int x = 0; x < board.Rows; ++x)
            {
                for (int y = 0; y < board.Columns; ++y)
                {
                    fixedBoard[x, y] = board.Fields[x, y].IsBomb;
                }
            }
            return true;
        }
        fixedBoard = null;
        return false;
    }

}
