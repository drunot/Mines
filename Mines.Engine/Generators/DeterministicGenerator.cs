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
/// bombs and the solver is tried again, up to <see cref="FixAttempts"/> times; after that, or if the
/// fixer fails, it starts over with a new random board.
/// </remarks>
public class DeterministicGenerator : IMatrixGenerator
{

    /// <summary>
    /// Creates the generator.
    /// </summary>
    /// <param name="random">
    /// The random number source, also used by the board fixer; a new <see cref="Random"/> if <c>null</c>.
    /// </param>
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
    IBoardFixer _fixer;

    /// <summary>
    /// Whether <paramref name="position"/> is within the 3x3 area around <paramref name="firstReveal"/>.
    /// </summary>
    /// <param name="firstReveal">The first revealed field.</param>
    /// <param name="position">The position to test.</param>
    /// <returns><c>true</c> if the position is in the start area.</returns>
    bool WithinStart(Point<int> firstReveal, Point<int> position)
    {
        if (position.X >= firstReveal.X - 1 &&
           position.X <= firstReveal.X + 1 &&
           position.Y >= firstReveal.Y - 1 &&
           position.Y <= firstReveal.Y + 1)
        {
            return true;
        }
        return false;
    }

    /// <summary>How many times a board is fixed and solved again before starting over.</summary>
    const int FixAttempts = 40;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// There are not enough cells outside the start area for <paramref name="bombCount"/> bombs.
    /// </exception>
    public void Generate(FieldMatrix fieldMatrix, Point<int> firstReveal, int bombCount)
    {
        while (true)
        {
            var bombs = new bool[fieldMatrix.Rows, fieldMatrix.Columns];

            int[] cells = Enumerable.Range(0, fieldMatrix.Rows * fieldMatrix.Columns).ToArray();
            _rand.Shuffle(cells);
            int placed = 0;
            for (int j = 0; j < cells.Length && placed < bombCount; ++j)
            {
                var point = new Point<int>(cells[j] / fieldMatrix.Columns, cells[j] % fieldMatrix.Columns);
                if (WithinStart(firstReveal, point))
                {
                    continue;   // keep the start area free; try the next cell
                }
                bombs[point.X, point.Y] = true;
                ++placed;
            }
            if (placed < bombCount)
            {
                throw new ArgumentException("Not enough cells outside the start area for that many bombs.");
            }

            for (int i = 0; i < FixAttempts; i++)
            {
                var matrix = new FieldMatrix(bombs);
                matrix[firstReveal.X, firstReveal.Y].Hidden = false;
                _solver.Solve(matrix);

                if (matrix.GameWon)
                {
                    fieldMatrix.ApplyBombs(bombs);
                    return;
                }

                if (_fixer.FixBoard(matrix, out var tmpBombs))
                {
                    bombs = tmpBombs;
                }
                else
                {
                    break; // The fixer failed try new board.
                }

            }
        }
    }
}
