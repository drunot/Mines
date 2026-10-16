using System.Collections.Generic;

namespace Mines.Solving;

/// <summary>
/// The last-resort strategy: tries every possible bomb layout of a group of connected equations and
/// decides the cells that are safe, or a bomb, in all of them.
/// </summary>
class EnumerationStrategy : SolvingStrategyBase
{
    /// <summary>
    /// Takes the next changed group (or, failing that, the global group), enumerates its solutions and
    /// reveals or flags the cells that are the same in every solution.
    /// </summary>
    /// <param name="fieldMatrix">The board to change.</param>
    /// <param name="equations">The equations to take the group from and update.</param>
    /// <returns>
    /// <c>true</c> if a group was processed (even if nothing could be decided); <c>false</c> if no group
    /// was left.
    /// </returns>
    public override bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations)
    {
        // Changed groups first; the global group is the last resort.
        if (!equations.TryTakeEnumerationGroup(out HashSet<Equation>? equationGroup, out HashSet<int>? cells) &&
            !equations.TryTakeGlobalGroup(out equationGroup, out cells))
        {
            return false;
        }

        var group = new Group(equationGroup, cells);
        group.Enumerate();

        if (group.SolutionCount == 0)
        {
            return true;   // contradiction: decide nothing
        }

        for (int i = 0; i < group.Cells.Length; i++)
        {
            if (group.MineCount[i] == 0)
            {
                Reveal(fieldMatrix, equations, group.Cells[i]);
            }
            else if (group.MineCount[i] == group.SolutionCount)
            {
                Flag(fieldMatrix, equations, group.Cells[i]);
            }
        }

        return true;
    }

    /// <summary>The value assigned to a cell during the search.</summary>
    private enum FieldValues : byte
    {
        /// <summary>Not yet assigned.</summary>
        Unknown = 0, // the default, so a new array starts all Unknown
        /// <summary>Assigned no bomb.</summary>
        Safe = 1,
        /// <summary>Assigned a bomb.</summary>
        Bomb = 2,
    }

    /// <summary>An equation in terms of positions in <see cref="Group.Cells"/>.</summary>
    /// <param name="Vars">The positions in <see cref="Group.Cells"/> of the equation's cells.</param>
    /// <param name="Sum">The number of bombs among those cells.</param>
    private record EquationRecord(int[] Vars, int Sum);

    /// <summary>
    /// One group converted to plain arrays, so the search avoids dictionaries and hashing.
    /// </summary>
    private sealed class Group
    {
        /// <summary>
        /// Converts a group of equations and their cells into arrays ready for <see cref="Enumerate"/>.
        /// </summary>
        /// <param name="equationGroup">The equations of the group.</param>
        /// <param name="cells">The cells (<see cref="Field.Index"/> values) appearing in those equations.</param>
        public Group(HashSet<Equation> equationGroup, HashSet<int> cells)
        {
            Cells = OrderCells(equationGroup, cells);

            var index = new Dictionary<int, int>();   // cell -> position in Cells, used only during setup
            for (int i = 0; i < Cells.Length; ++i)
            {
                index[Cells[i]] = i;
            }

            // The equations of this group, written in terms of cell indices, not cells.
            _eqs = new EquationRecord[equationGroup.Count];
            int k = 0;
            foreach (Equation e in equationGroup)
            {
                var vars = new int[e.Count];
                for (int v = 0; v < e.Count; v++)
                {
                    vars[v] = index[e.Cells[v]];
                }
                _eqs[k++] = new EquationRecord(vars, e.Sum);
            }
            _eqsOfVar = EquationsOfEachCell(_eqs, Cells.Length);
            _values = new FieldValues[Cells.Length];   // all Unknown (the default)
            MineCount = new int[Cells.Length];
        }

        /// <summary>
        /// Position -&gt; cell (<see cref="Field.Index"/>), ordered so cells that share equations are close together.
        /// </summary>
        public int[] Cells { get; }

        /// <summary>
        /// <c>MineCount[i]</c>: in how many solutions <c>Cells[i]</c> was a bomb.
        /// </summary>
        public int[] MineCount { get; }

        /// <summary>The number of solutions found by <see cref="Enumerate"/>.</summary>
        public int SolutionCount { get; private set; }

        /// <summary>
        /// Finds all bomb layouts that satisfy every equation of the group, filling in
        /// <see cref="SolutionCount"/> and <see cref="MineCount"/>.
        /// </summary>
        public void Enumerate() => Search(0);

        /// <summary>
        /// Backtracking search: assigns cell <paramref name="i"/> and recurses into the next cell; counts a
        /// solution once every cell is assigned.
        /// </summary>
        /// <param name="i">The position in <see cref="Cells"/> to assign next.</param>
        private void Search(int i)
        {
            if (i == Cells.Length)
            {
                SolutionCount++;
                int decidable = 0;
                for (int v = 0; v < Cells.Length; v++)
                {
                    MineCount[v] += _values[v] == FieldValues.Bomb ? 1 : 0;
                    if (MineCount[v] == 0 || MineCount[v] == SolutionCount)
                    {
                        decidable++;   // so far always safe, or always a bomb
                    }
                }
                // A cell that differed between two solutions can never be decided, so once every cell has,
                // further solutions can't change the result.
                _stop = decidable == 0;
                return;
            }

            for (byte choice = (byte)FieldValues.Safe; choice <= (byte)FieldValues.Bomb && !_stop; choice++)
            {
                _values[i] = (FieldValues)choice;
                if (StillPossible(i))
                {
                    Search(i + 1);
                }
            }
            _values[i] = FieldValues.Unknown;   // undo before returning to the caller
        }

        /// <summary>
        /// Can every equation containing cell <paramref name="v"/> still be satisfied?
        /// </summary>
        /// <param name="v">The position in <see cref="Cells"/> that was just assigned.</param>
        /// <returns><c>true</c> if no equation containing it is already violated.</returns>
        private bool StillPossible(int v)
        {
            foreach (int e in _eqsOfVar[v])
            {
                int sum = 0;        // bombs assigned so far in this equation
                int unassigned = 0; // cells not yet decided

                foreach (int u in _eqs[e].Vars)
                {
                    if (_values[u] == FieldValues.Unknown)
                    {
                        unassigned++;
                    }
                    else
                    {
                        sum += _values[u] == FieldValues.Bomb ? 1 : 0;
                    }
                }

                if (sum > _eqs[e].Sum)
                {
                    return false;   // too many bombs already
                }
                if (sum + unassigned < _eqs[e].Sum)
                {
                    return false;   // can't reach the target any more
                }
            }
            return true;
        }

        /// <summary>
        /// Number the cells in the order a search through the equations reaches them.
        /// </summary>
        /// <param name="equationGroup">The equations of the group.</param>
        /// <param name="cells">The cells of the group.</param>
        /// <returns>The cells (<see cref="Field.Index"/> values) in breadth-first order.</returns>
        private static int[] OrderCells(HashSet<Equation> equationGroup, HashSet<int> cells)
        {
            // Cell -> the group's equations that contain it.
            var eqsOfCell = new Dictionary<int, List<Equation>>();
            foreach (Equation e in equationGroup)
            {
                foreach (int p in e.Cells)
                {
                    if (!eqsOfCell.TryGetValue(p, out List<Equation>? list))
                    {
                        list = new List<Equation>();
                        eqsOfCell[p] = list;
                    }
                    list.Add(e);
                }
            }

            var order = new List<int>(cells.Count);
            var seen = new HashSet<int>();
            foreach (int start in cells)   // more than one start in case the group isn't connected
            {
                if (!seen.Add(start))
                {
                    continue;
                }
                var queue = new Queue<int>();
                queue.Enqueue(start);
                while (queue.TryDequeue(out int p))
                {
                    order.Add(p);
                    foreach (Equation e in eqsOfCell[p])
                    {
                        if (e.Count > 8)
                        {
                            continue;   // the global equation and its large differences: they'd link everything
                        }
                        foreach (int q in e.Cells)
                        {
                            if (seen.Add(q))
                            {
                                queue.Enqueue(q);
                            }
                        }
                    }
                }
            }
            return order.ToArray();
        }

        /// <summary>
        /// For each cell index, the indices of the equations that contain it.
        /// </summary>
        /// <param name="eqs">The equations of the group.</param>
        /// <param name="cellCount">The number of cells in the group.</param>
        /// <returns>
        /// For each position in <see cref="Cells"/>, the indices into <paramref name="eqs"/> of the equations
        /// containing it.
        /// </returns>
        private static int[][] EquationsOfEachCell(EquationRecord[] eqs, int cellCount)
        {
            var count = new int[cellCount];
            foreach (EquationRecord eq in eqs)
            {
                foreach (int v in eq.Vars)
                {
                    count[v] += 1;
                }
            }

            var eqsOfVar = new int[cellCount][];
            for (int v = 0; v < cellCount; v++)
            {
                eqsOfVar[v] = new int[count[v]];
            }

            for (int e = 0; e < eqs.Length; e++)
            {
                foreach (int v in eqs[e].Vars)
                {
                    eqsOfVar[v][--count[v]] = e;
                }
            }
            return eqsOfVar;
        }

        /// <summary>The equations of this group, in terms of positions in <see cref="Cells"/>.</summary>
        private readonly EquationRecord[] _eqs;
        /// <summary>
        /// For each position in <see cref="Cells"/>, the indices into <see cref="_eqs"/> of the equations
        /// containing it.
        /// </summary>
        private readonly int[][] _eqsOfVar;
        /// <summary>The current assignment of each position in <see cref="Cells"/> during the search.</summary>
        private readonly FieldValues[] _values;
        /// <summary>Set when no cell can be decided any more, so the search stops early.</summary>
        private bool _stop = false;

    }
}
