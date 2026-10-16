using System.Collections.Generic;

namespace Mines.Solving;

/// <summary>
/// Solves a board as far as possible without guessing, by treating every revealed number as an
/// <see cref="Equation"/> over its hidden neighbours.
/// </summary>
/// <remarks>
/// Runs <see cref="NeighborStrategy"/> until it stalls, then <see cref="SubsetStrategy"/>, then
/// <see cref="EnumerationStrategy"/> as a last resort; whenever a later step changes something it goes
/// back to the first. Stops when none of them can make progress.
/// </remarks>
public class ConstraintSolver : IResumableSolver
{
    /// <summary>
    /// Whether the global equation (the number of bombs left) may be used. Turn it off when solving a part of
    /// a larger board, such as a window of an infinite board.
    /// </summary>
    public bool UseGlobalEquation { get; init; } = true;

    /// <summary>
    /// Whether revealed cells on the board's edge give no equation. Turn it on when solving a part of a larger
    /// board: such cells have neighbours outside the part, so their number here isn't the one the player sees.
    /// </summary>
    public bool IgnoreEdges { get; init; }

    /// <summary>
    /// Builds an equation for every revealed field that gives one (see <see cref="EquationList.GivesEquation"/>):
    /// its hidden, unflagged neighbours add up to the number of hidden, unflagged bombs around it. A 0 gives an
    /// empty equation, which <see cref="EquationList.Add"/> ignores.
    /// </summary>
    /// <param name="matrix">The board to read.</param>
    /// <returns>
    /// A new <see cref="EquationList"/> holding the equations, with <see cref="UseGlobalEquation"/> and
    /// <see cref="IgnoreEdges"/> passed on.
    /// </returns>
    EquationList CollectEquations(FieldMatrix matrix)
    {
        EquationList returnValue =
            new EquationList(matrix) { UseGlobalEquation = UseGlobalEquation, IgnoreEdges = IgnoreEdges };
        foreach (Field f in matrix.Fields)
        {
            if (f.Hidden || !returnValue.GivesEquation(f))
            {
                continue;
            }
            var equation = new Equation();
            foreach (Field neighbor in f.Neighbors)
            {
                if (neighbor.Hidden && !neighbor.Flagged)
                {
                    equation.Add(neighbor.Index);
                }
                if (neighbor.IsBomb && neighbor.Hidden && !neighbor.Flagged)
                {
                    equation.Sum += 1;  // f.Number minus flags and revealed bombs, counted here to avoid a second pass.
                }
            }
            returnValue.Add(equation);
        }
        return returnValue;
    }

    /// <summary>The steps of the solver's state machine, in order of increasing cost.</summary>
    enum SolverStates
    {
        /// <summary>Apply <see cref="NeighborStrategy"/>.</summary>
        NeighborSolver,
        /// <summary>Apply <see cref="SubsetStrategy"/>.</summary>
        SubsetSolver,
        /// <summary>Apply <see cref="EnumerationStrategy"/>.</summary>
        EnumerationSolver,
        /// <summary>No strategy made progress; stop.</summary>
        Exit,
    }
    /// <summary>The strategy to run in each state.</summary>
    Dictionary<SolverStates, ISolvingStrategy> _solvingStrategies = new Dictionary<SolverStates, ISolvingStrategy>
    {
        {SolverStates.EnumerationSolver, new EnumerationStrategy()},
        {SolverStates.SubsetSolver, new SubsetStrategy()},
        {SolverStates.NeighborSolver, new NeighborStrategy()},
    };

    /// <summary>
    /// Reveals and flags every field that can be deduced without guessing. Changes
    /// <paramref name="fieldMatrix"/> in place.
    /// </summary>
    /// <param name="fieldMatrix">The board to solve; at least one field should already be revealed.</param>
    public void Solve(FieldMatrix fieldMatrix)
    {
        _matrix = fieldMatrix;
        _equations = CollectEquations(_matrix);
        Solve();
    }

    /// <summary>
    /// Runs the strategies on <see cref="_matrix"/> with <see cref="_equations"/> until none of them makes
    /// progress.
    /// </summary>
    void Solve()
    {
        if (_matrix is null || _equations is null)
        {
            return;
        }

        SolverStates states = SolverStates.NeighborSolver;
        while (true)
        {
            _equations.TryCreateGlobalEquation();

            switch (states)
            {
                // Step 1: Simple neighbor solver.
                case SolverStates.NeighborSolver:
                    {
                        bool boardChanged =
                            _solvingStrategies[SolverStates.NeighborSolver].ApplyStrategy(_matrix, _equations);
                        if (!boardChanged)
                        {
                            states = SolverStates.SubsetSolver;
                        }
                        break;
                    }
                // Step 2: Simpler the full constraint check. (Subset check.)
                case SolverStates.SubsetSolver:
                    {
                        bool boardChanged =
                            _solvingStrategies[SolverStates.SubsetSolver].ApplyStrategy(_matrix, _equations);
                        if (!boardChanged)
                        {
                            states = SolverStates.EnumerationSolver;
                        }
                        else
                        {
                            states = SolverStates.NeighborSolver;
                        }
                        break;
                    }
                // Step 3: Enumeration Solver. The most involved and last resort.
                case SolverStates.EnumerationSolver:
                    {
                        bool boardChanged =
                            _solvingStrategies[SolverStates.EnumerationSolver].ApplyStrategy(_matrix, _equations);
                        if (!boardChanged)
                        {
                            states = SolverStates.Exit;
                        }
                        else
                        {
                            states = SolverStates.NeighborSolver;
                        }
                        break;
                    }
                case SolverStates.Exit:
                    {
                        return;
                    }
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The swaps change revealed numbers, so all equations are rebuilt from the board as it is now (what a
    /// player would see) rather than patched. Does nothing if <see cref="Solve(FieldMatrix)"/> hasn't been
    /// called yet.
    /// </remarks>
    public void SwapAndContinue(List<(Point<long>, Point<long>)> swaps)
    {
        if (_matrix is null || _equations is null)
        {
            return;
        }

        foreach (var swap in swaps)
        {
            _matrix.SwapBombs(swap.Item1, swap.Item2);
        }
        _equations = CollectEquations(_matrix);
        Solve();
    }

    /// <summary>The board of the last <see cref="Solve(FieldMatrix)"/> call; <c>null</c> before the first.</summary>
    private FieldMatrix? _matrix;
    /// <summary>The equations for <see cref="_matrix"/>; <c>null</c> before the first solve.</summary>
    private EquationList? _equations;

}
