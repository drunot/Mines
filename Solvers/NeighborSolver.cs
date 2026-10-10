using System.Linq;

namespace Mines.Solving;


class NeighborSolver : ISolver
{
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

                int hidden_count = field.Neighbors.Count((Field f) => f.Hidden);
                int flagged_count = field.Neighbors.Count((Field f) => f.Flagged);

                if (field.Number == hidden_count)
                {

                    foreach(Field f in field.Neighbors)
                    {
                        if (f.Hidden && !f.Flagged) { f.Flagged = true; changed = true; }
                    }
                }

                if (field.Number == flagged_count)
                {
                    foreach(Field f in field.Neighbors)
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