
using System.Collections.Generic;
using System.Linq;
using System;

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
    /// Builds an equation for every revealed field: its hidden, unflagged neighbours add up to the number
    /// of unflagged bombs around it.
    /// </summary>
    /// <param name="matrix">The board to read.</param>
    /// <returns>A new <see cref="EquationList"/> holding the equations.</returns>
    static EquationList CollectEquations(FieldMatrix matrix)
    {
        EquationList return_value = new EquationList(matrix);
        foreach (Field f in matrix.Fields)
        {
            if (f.Hidden)
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
                if (neighbor.IsBomb && !neighbor.Flagged)
                {
                    equation.sum += 1;  // equals f.Number - Flagged, counted here to avoid a second pass.
                }
            }
            return_value.Add(equation);
        }
        return return_value;
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
    Dictionary<SolverStates, ISolvingStrategy> solvingStrategies = new Dictionary<SolverStates, ISolvingStrategy>
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
                        bool board_changed = solvingStrategies[SolverStates.NeighborSolver].ApplyStrategy(_matrix, _equations);
                        if (!board_changed)
                        {
                            states = SolverStates.SubsetSolver;
                        }
                        break;
                    }
                // Step 2: Simpler the full constraint check. (Subset check.)
                case SolverStates.SubsetSolver:
                    {
                        bool board_changed = solvingStrategies[SolverStates.SubsetSolver].ApplyStrategy(_matrix, _equations);
                        if (!board_changed)
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
                        bool board_changed = solvingStrategies[SolverStates.EnumerationSolver].ApplyStrategy(_matrix, _equations);
                        if (!board_changed)
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
    public void SwapAndContinue(List<(Point<int>, Point<int>)> swaps)
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