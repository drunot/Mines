using System.Collections.Generic;
using System.Linq;

namespace Mines.Solving;


class EnumerationStrategy : SolvingStrategyBase
{
    public override bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations)
    {
        // Changed groups first; the global group is the last resort.
        if (!equations.TryTakeEnumerationGroup(out HashSet<Equation>? equation_group, out HashSet<Point<int>>? cells) &&
            !equations.TryTakeGlobalGroup(out equation_group, out cells))
        {
            return false;
        }

        var group = new Group(equation_group, cells);
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

    private enum FieldValues : byte
    {
        Unknown = 0, // the default, so a new array starts all Unknown
        Safe = 1,
        Bomb = 2,
    }

    private record EquationRecord(int[] Vars, int Sum);

    // One group converted to plain arrays, so the search avoids dictionaries and hashing.
    private sealed class Group
    {
        public Group(HashSet<Equation> equation_group, HashSet<Point<int>> cells)
        {
            Cells = OrderCells(equation_group, cells);

            var index = new Dictionary<Point<int>, int>();   // cell -> index, used only during setup
            for (int i = 0; i < Cells.Length; ++i)
            {
                index[Cells[i]] = i;
            }

            // The equations of this group, written in terms of cell indices, not cells.
            _eqs = equation_group.Select((e) => new EquationRecord(e.variables.Select((v) => index[v]).ToArray(), e.sum)).ToArray();
            _eqs_of_var = EquationsOfEachCell(_eqs, Cells.Length);
            _values = new FieldValues[Cells.Length];   // all Unknown (the default)
            MineCount = new int[Cells.Length];
        }

        // Index -> cell, numbered so cells that share equations are close together.
        public Point<int>[] Cells { get; }

        // MineCount[i]: in how many solutions Cells[i] was a bomb.
        public int[] MineCount { get; }

        public int SolutionCount { get; private set; }

        public void Enumerate() => Search(0);

        private void Search(int i)
        {
            if (i == Cells.Length)
            {
                SolutionCount++;
                int undecided = 0;
                for (int v = 0; v < Cells.Length; v++)
                {
                    MineCount[v] += _values[v] == FieldValues.Bomb ? 1 : 0;
                    if (MineCount[v] == 0 || MineCount[v] == SolutionCount)
                    {
                        undecided++;   // so far always safe, or always a bomb
                    }
                }
                _stop = undecided == 0;
                return;
            }

            for (byte choice = (byte)FieldValues.Safe; choice <= (byte)FieldValues.Bomb; choice++)
            {
                _values[i] = (FieldValues)choice;
                if (StillPossible(i))
                {
                    Search(i + 1);
                }
            }
            _values[i] = FieldValues.Unknown;   // undo before returning to the caller
        }

        // Can every equation containing cell v still be satisfied?
        private bool StillPossible(int v)
        {
            foreach (int e in _eqs_of_var[v])
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

        // Number the cells in the order a search through the equations reaches them.
        private static Point<int>[] OrderCells(HashSet<Equation> equation_group, HashSet<Point<int>> cells)
        {
            // Cell -> the group's equations that contain it.
            var eqs_of_cell = new Dictionary<Point<int>, List<Equation>>();
            foreach (Equation e in equation_group)
            {
                foreach (Point<int> p in e.variables)
                {
                    if (!eqs_of_cell.TryGetValue(p, out List<Equation>? list))
                    {
                        list = new List<Equation>();
                        eqs_of_cell[p] = list;
                    }
                    list.Add(e);
                }
            }

            var order = new List<Point<int>>(cells.Count);
            var seen = new HashSet<Point<int>>();
            foreach (Point<int> start in cells)   // more than one start in case the group isn't connected
            {
                if (!seen.Add(start))
                {
                    continue;
                }
                var queue = new Queue<Point<int>>();
                queue.Enqueue(start);
                while (queue.TryDequeue(out Point<int>? p))
                {
                    order.Add(p);
                    foreach (Equation e in eqs_of_cell[p])
                    {
                        if (e.variables.Count > 8)
                        {
                            continue;   // the global equation and its large differences: they'd link everything
                        }
                        foreach (Point<int> q in e.variables)
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

        // For each cell index, the indices of the equations that contain it.
        private static int[][] EquationsOfEachCell(EquationRecord[] eqs, int cell_count)
        {
            var count = new int[cell_count];
            foreach (EquationRecord eq in eqs)
            {
                foreach (int v in eq.Vars)
                {
                    count[v] += 1;
                }
            }

            var eqs_of_var = new int[cell_count][];
            for (int v = 0; v < cell_count; v++)
            {
                eqs_of_var[v] = new int[count[v]];
            }

            for (int e = 0; e < eqs.Length; e++)
            {
                foreach (int v in eqs[e].Vars)
                {
                    eqs_of_var[v][--count[v]] = e;
                }
            }
            return eqs_of_var;
        }

        private readonly EquationRecord[] _eqs;
        private readonly int[][] _eqs_of_var;
        private readonly FieldValues[] _values;
        private bool _stop = false;

    }
}
