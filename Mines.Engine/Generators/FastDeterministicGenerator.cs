using System;
using Mines.Solving;
using System.Linq;
using Mines.BoardFixer;
using System.Diagnostics.CodeAnalysis;

namespace Mines.Generators;


/// <summary>
/// Generates boards that can be solved without guessing from the first reveal. A faster version of
/// <see cref="DeterministicGenerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Places bombs at random outside the 3x3 area around the first reveal, then runs
/// <see cref="ConstraintSolver"/>. If the board can't be solved, <see cref="EdgeBoardFixer"/> moves
/// bombs and the solver continues from where it got stuck (<see cref="IResumableSolver.SwapAndContinue"/>),
/// up to <see cref="fix_attempts"/> times; after that, or if the fixer fails, it starts over with a new
/// random board.
/// </para>
/// <para>
/// Continuing keeps the cells revealed and flagged before a swap, but some of them were deduced from numbers
/// the swap changed, so the result isn't always solvable from the first click (the BruteForce project finds
/// such boards). A board fixed this way is therefore solved once more from the first click before it is
/// returned; if that gets stuck, fixing continues from that fresh position, which a player can reach.
/// A board that needed no fixing was already solved from the first click and is returned straight away.
/// </para>
/// </remarks>
public class FastDeterministicGenerator : IMatrixGenerator
{

    /// <summary>
    /// Creates the generator.
    /// </summary>
    /// <param name="random">The random number source, also used by the board fixer; a new <see cref="Random"/> if <c>null</c>.</param>
    public FastDeterministicGenerator(Random? random = null)
    {
        _rand = random ?? new Random();
        _fixer = new EdgeBoardFixer(_rand);
    }

    /// <summary>The random number source.</summary>
    Random _rand;

    /// <summary>Checks whether a generated board can be solved without guessing.</summary>
    IResumableSolver _solver = new ConstraintSolver();
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

    /// <summary>
    /// Copies the bomb layout of <paramref name="matrix"/> into <paramref name="bombs"/>.
    /// </summary>
    /// <param name="matrix">The board to read.</param>
    /// <param name="bombs">Receives the layout; <c>[row, column]</c> is true for a bomb. Must have the board's size.</param>
    private static void GetBombs(FieldMatrix matrix, bool[,] bombs )
    {
            for (int x = 0; x < matrix.Rows; ++x)
            {
                for (int y = 0; y < matrix.Columns; ++y)
                {
                    bombs[x, y] = matrix.Fields[x, y].IsBomb;
                }
            }
    }

    /// <summary>
    /// Solves the current bomb layout of <paramref name="matrix"/> again from the first click only, to check
    /// that it can be solved without the deductions made before earlier swaps.
    /// </summary>
    /// <param name="matrix">
    /// The board the fast path won. Replaced with the fresh board, which the solver now works on, so fixing
    /// can continue from it if the check fails.
    /// </param>
    /// <param name="bombs">Receives the layout if the check succeeds.</param>
    /// <param name="first_reveal">The first revealed field.</param>
    /// <returns><c>true</c> if the fresh board was solved completely.</returns>
    private bool TryVerify(ref FieldMatrix matrix, bool[,] bombs, Point<int> first_reveal)
    {
        matrix = new FieldMatrix(matrix);
        matrix[first_reveal.X, first_reveal.Y].Hidden = false;
        _solver.Solve(matrix);

        if (matrix.GameWon)
        {
            GetBombs(matrix, bombs);
            return true;
        }
        return false;
    }

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

            var matrix = new FieldMatrix(bombs);
            matrix[first_reveal.X, first_reveal.Y].Hidden = false;
            _solver.Solve(matrix);

            if (matrix.GameWon)
            {
                GetBombs(matrix, bombs);

                return bombs;
            }
            for (int i = 0; i < fix_attempts; i++)
            {

                if (matrix.GameWon)
                {
                    if (TryVerify(ref matrix, bombs, first_reveal))
                    {
                        return bombs;
                    }
                }


                if (_fixer.GetBoardChanges(matrix, out var changes))
                {
                    _solver.SwapAndContinue(changes);
                }
                else
                {
                    break; // The fixer failed try new board.
                }

            }
            if (matrix.GameWon)
            {


                // Ensure that the game is solvable.
                // The fast solver can create boards in rare cases that aren't.
                if (TryVerify(ref matrix, bombs, first_reveal))
                {
                    return bombs;
                }
            }
        }
    }
}