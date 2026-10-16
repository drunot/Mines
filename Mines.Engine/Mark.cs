namespace Mines;

/// <summary>
/// The player's mark on a hidden <see cref="Field"/>.
/// </summary>
public enum Mark
{
    /// <summary>No mark.</summary>
    None,
    /// <summary>A flag: the player says it's a bomb. Counts for the bombs left, and can't be revealed by a click.</summary>
    Flag,
    /// <summary>A question mark: the player's note that it might be a bomb. Counts for nothing.</summary>
    Question,
}
