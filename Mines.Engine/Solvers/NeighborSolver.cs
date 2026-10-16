using System.Linq;

namespace Mines.Solving;

/// <summary>
/// A simple solver that only looks at single numbers: flags all hidden neighbours if their count
/// equals the number, and reveals the unflagged ones if the flags already account for the number.
/// </summary>
class NeighborSolver : ISolver
{
    /// <inheritdoc/>
    public void Solve(FieldMatrix fieldMatrix)
    {
        while (true)
        {
            var changed = false;
            foreach (Field field in fieldMatrix.Fields)
            {
                if (field.Hidden || field.Number is null)
                {
                    continue;
                }

                int hiddenCount = field.Neighbors.Count(f => f.Hidden);
                int flaggedCount = field.Neighbors.Count(f => f.Flagged);

                if (field.Number == hiddenCount)
                {

                    foreach (Field f in field.Neighbors)
                    {
                        if (f.Hidden && !f.Flagged) { f.Flagged = true; changed = true; }
                    }
                }

                if (field.Number == flaggedCount)
                {
                    foreach (Field f in field.Neighbors)
                    {
                        if (f.Hidden && !f.Flagged) { f.Hidden = false; changed = true; }
                    }
                }
            }
            if (!changed)
            {
                break;
            }
        }

    }
}
