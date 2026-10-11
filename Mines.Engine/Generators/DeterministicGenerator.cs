using System;
using Mines.Solving;
using System.Linq;
using Mines.BoardFixer;

namespace Mines.Generators;


/// <summary>
/// Generates boards that can be solved without guessing from the first reveal.
/// </summary>
/// <remarks>
/// Places bombs at random outside the 3x3 area around the first reveal, then runs
/// <see cref="ConstraintSolver"/>. If the board can't be solved, <see cref="EdgeBoardFixer"/> moves
/// bombs and the solver is tried again, up to <see cref="fix_attempts"/> times; after that, or if the
/// fixer fails, it starts over with a new random board.
/// </remarks>
public class DeterministicGenerator : IMatrixGenerator
{

    /// <summary>
    /// Creates the generator.
    /// </summary>
    /// <param name="random">The random number source, also used by the board fixer; a new <see cref="Random"/> if <c>null</c>.</param>
    public DeterministicGenerator(Random? random = null)
    {
        _rand = random ?? new Random();
        _fixer = new EdgeBoardFixer(_rand);
    }

    /// <summary>The random number source.</summary>
    Random _rand;

    /// <summary>Checks whether a generated board can be solved without guessing.</summary>
    ISolver _solver = new ConstraintSolver();
    /// <summary>Moves bombs on boards the solver got stuck on.</summary>
    iBoardFixer _fixer;

    /// <summary>
    /// Whether <paramref name="position"/> is within the 3x3 area around <paramref name="first_reveal"/>.
    /// </summary>
    /// <param name="first_reveal">The first revealed field.</param>
    /// <param name="position">The position to test.</param>
    /// <returns><c>true</c> if the position is in the start area.</returns>
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

    /// <summary>How many times a board is fixed and solved again before starting over.</summary>
    const int fix_attempts = 40;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">There are not enough cells outside the start area for <paramref name="bomb_count"/> bombs.</exception>
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