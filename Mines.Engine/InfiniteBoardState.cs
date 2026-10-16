using System.Collections.Generic;

namespace Mines;

/// <summary>
/// Everything needed to continue an <see cref="InfiniteFieldMatrix"/> later: its chunks and counters. Plain data,
/// so it can be written as JSON. Made by <see cref="InfiniteFieldMatrix.Save"/> and read by
/// <see cref="InfiniteFieldMatrix.Load"/>.
/// </summary>
public sealed class InfiniteBoardState
{
    /// <summary>Every chunk created so far.</summary>
    public List<ChunkState> Chunks { get; set; } = new();

    /// <summary>The safe clicks in a row (<see cref="InfiniteFieldMatrix.Streak"/>).</summary>
    public int Streak { get; set; }

    /// <summary>The longest streak in this game (<see cref="InfiniteFieldMatrix.BestStreak"/>).</summary>
    public int BestStreak { get; set; }

    /// <summary>How many bombs have been hit (<see cref="InfiniteFieldMatrix.BombsHit"/>).</summary>
    public int BombsHit { get; set; }

    /// <summary>Whether the last action hit a bomb (<see cref="InfiniteFieldMatrix.LastActionHitBomb"/>).</summary>
    public bool LastActionHitBomb { get; set; }
}

/// <summary>
/// One chunk of a saved infinite board.
/// </summary>
public sealed class ChunkState
{
    /// <summary>The chunk's row among the chunks.</summary>
    public long Row { get; set; }

    /// <summary>The chunk's column among the chunks.</summary>
    public long Column { get; set; }

    /// <summary>
    /// Whether the chunk has been part of a solved window, so its layout is no longer reshuffled.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Whether the chunk has been the centre of a window, so it has been arranged to be solvable by logic.
    /// </summary>
    public bool CentreChecked { get; set; }

    /// <summary>
    /// One letter per cell, row by row: <c>'a'</c> plus a sum of 1 for a bomb, 2 for revealed, 4 for a flag and
    /// 8 for a question mark (so <c>'a'</c> to <c>'p'</c>).
    /// </summary>
    public string Cells { get; set; } = "";
}
