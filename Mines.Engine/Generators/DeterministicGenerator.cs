using System;
using Mines.Solving;
using System.Linq;
using Mines.BoardFixer;

namespace Mines.Generators;


public class DeterministicGenerator : IMatrixGenerator
{

    public DeterministicGenerator(Random? random = null)
    {
        _rand = random ?? new Random();
        _fixer = new EdgeBoardFixer(_rand);
    }

    Random _rand;

    ISolver _solver = new ConstraintSolver();
    iBoardFixer _fixer;

    bool WithinStart(Point<int> first_reveal, Point<int> position)
    {
        if (position.X >= first_reveal.X - 1 &&
           position.X <= first_reveal.X + 1 &&
           position.Y >= first_reveal.Y - 1 &&
           position.Y <= first_reveal.Y + 1)
        {
            return true;
        }
        return false;
    }

    const int fix_attempts = 40;

    public bool[,] Generate(int rows, int columns, Point<int> first_reveal, int bomb_count)
    {
        while (true)
        {
            var bombs = new bool[rows, columns];

            int[] cells = Enumerable.Range(0, rows * columns).ToArray();
            _rand.Shuffle(cells);
            int placed = 0;
            for (int j = 0; j < cells.Length && placed < bomb_count; ++j)
            {
                var point = new Point<int>(cells[j] / columns, cells[j] % columns);
                if (WithinStart(first_reveal, point))
                {
                    continue;   // keep the start area free; try the next cell
                }
                bombs[point.X, point.Y] = true;
                ++placed;
            }
            if (placed < bomb_count)
            {
                throw new ArgumentException("Not enough cells outside the start area for that many bombs.");
            }

            for (int i = 0; i < fix_attempts; i++)
            {
                var matrix = new FieldMatrix(bombs);
                matrix[first_reveal.X, first_reveal.Y].Hidden = false;
                _solver.Solve(matrix);

                if (matrix.GameWon)
                {
                    return bombs;
                }


                if (_fixer.FixBoard(matrix, out var tmp_bombs))
                {
                    bombs = tmp_bombs;
                }
                else
                {
                    break; // The fixer failed try new board.
                }

            }
        }
    }
}