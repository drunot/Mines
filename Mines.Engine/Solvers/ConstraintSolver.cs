
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
public class ConstraintSolver : ISolver
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
        var equations = CollectEquations(fieldMatrix);

        SolverStates states = SolverStates.NeighborSolver;
        while (true)
        {
            equations.TryCreateGlobalEquation();

            switch (states)
            {
                // Step 1: Simple neighbor solver.
                case SolverStates.NeighborSolver:
                    {
                        bool board_changed = solvingStrategies[SolverStates.NeighborSolver].ApplyStrategy(fieldMatrix, equations);
                        if (!board_changed)
                        {
                            states = SolverStates.SubsetSolver;
                        }
                        break;
                    }
                // Step 2: Simpler the full constraint check. (Subset check.)
                case SolverStates.SubsetSolver:
                    {
                        bool board_changed = solvingStrategies[SolverStates.SubsetSolver].ApplyStrategy(fieldMatrix, equations);
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
                        bool board_changed = solvingStrategies[SolverStates.EnumerationSolver].ApplyStrategy(fieldMatrix, equations);
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
}