

namespace Mines.Solving;

/// <summary>
/// Solves a board as far as it can, by revealing and flagging fields in place.
/// </summary>
public interface ISolver
{
    
    /// <summary>
    /// Reveals and flags the fields of <paramref name="fieldMatrix"/> that can be deduced.
    /// </summary>
    /// <param name="fieldMatrix">The board to solve.</param>
    void Solve(FieldMatrix fieldMatrix);

}