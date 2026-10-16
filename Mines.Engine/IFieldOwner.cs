namespace Mines;
/// <summary>
/// The board a <see cref="Field"/> belongs to: finds fields by global position (for neighbours) and is told
/// when a field is revealed or its flag changes.
/// </summary>
public interface IFieldOwner
{
    /// <summary>
    /// Gets the field at a global position, for a field's neighbours.
    /// </summary>
    /// <param name="row">The global row.</param>
    /// <param name="column">The global column.</param>
    /// <returns>The field, or <c>null</c> outside a finite board or in a chunk that doesn't exist yet.</returns>
    Field? FieldAt(long row, long column);
    /// <summary>
    /// Called after <see cref="Field.Hidden"/> of <paramref name="field"/> changed (revealed, or hidden again).
    /// </summary>
    /// <param name="field">The field that changed.</param>
    void OnRevealed(Field field);
    /// <summary>
    /// Called after <see cref="Field.Flagged"/> of <paramref name="field"/> changed.
    /// </summary>
    /// <param name="field">The field that changed; its new state is in <see cref="Field.Flagged"/>.</param>
    void OnFlagChanged(Field field);
}
