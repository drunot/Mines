using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Mines.BoardFixer;

public class EdgeBoardFixer : iBoardFixer
{

    class FieldCandidate
    {
        public const int ErrorScore = 99999;

        public FieldCandidate(Field field)
        {
            _field = field;
        }

        private Field _field;

        public Field Field
        {
            get => _field;
        }

        private int? _score = null;

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

        public bool ShouldTake
        {
            get => SelectionScore % 2 == 0;
        }

        public bool ShouldGive
        {
            get => SelectionScore != ErrorScore && SelectionScore % 2 == 1;
        }

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

    public EdgeBoardFixer(Random? random = null)
    {
        _rand = random ?? new Random();
    }

    private Random _rand;

    public bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixed_board)
    {
        List<FieldCandidate> candidates = new List<FieldCandidate>();
        foreach (Field f in board.Fields)
        {
            if (f.Number is not null && !f.Hidden)
            {
                candidates.Add(new FieldCandidate(f));
            }
        }
        candidates.Sort((f1, f2) => f1.SelectionScore.CompareTo(f2.SelectionScore));
        var best_candidate = candidates.First();

        if (best_candidate.SelectionScore == FieldCandidate.ErrorScore)
        {
            fixed_board = null;
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
                fixed_board = null;
                return false;
            }

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
                board.SwapBombs(fields_shuffled[i++].Position, fn.Position);
            }
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
                fixed_board = null;
                return false;
            }

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
                board.SwapBombs(fields_shuffled[i++].Position, fn.Position);
            }
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
}