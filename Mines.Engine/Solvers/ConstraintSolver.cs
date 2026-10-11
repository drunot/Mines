
using System.Collections.Generic;
using System.Linq;
using System;

namespace Mines.Solving;

public class ConstraintSolver : ISolver
{
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


    enum SolverStates
    {
        NeighborSolver,
        SubsetSolver,
        EnumerationSolver,
        Exit,
    }
    Dictionary<SolverStates, ISolvingStrategy> solvingStrategies = new Dictionary<SolverStates, ISolvingStrategy>
    {
        {SolverStates.EnumerationSolver, new EnumerationStrategy()},
        {SolverStates.SubsetSolver, new SubsetStrategy()},
        {SolverStates.NeighborSolver, new NeighborStrategy()},
    };


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