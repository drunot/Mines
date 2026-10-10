using System;
using System.Collections.Generic;
using System.Linq;

namespace Mines;

static class NeighborQuery
{
    extension(Field source)
    {
        public IEnumerable<Field> Neighbors
        {
            get
            {
                {
                    var pos = source.Position;
                    var parent = source.Parent;
                    var x_min = Math.Max(pos.X - 1, 0);
                    var x_max = Math.Min(pos.X + 1, parent.Rows - 1);
                    var y_min = Math.Max(pos.Y - 1, 0);
                    var y_max = Math.Min(pos.Y + 1, parent.Columns - 1);
                    for (int x_cor = x_min; x_cor <= x_max; ++x_cor)
                    {
                        for (int y_cor = y_min; y_cor <= y_max; ++y_cor)
                        {
                            yield return parent[x_cor, y_cor];
                        }
                    }
                }
            }
        }
    }

}
