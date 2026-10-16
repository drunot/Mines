using System;
using System.Numerics;

namespace Mines;
/// <summary>
/// A pair of coordinates. On the board, <see cref="X"/> is the row and <see cref="Y"/> the column.
/// </summary>
/// <typeparam name="T">The coordinate type.</typeparam>
public struct Point<T> : IEquatable<Point<T>> 
    where T : IEqualityOperators<T, T, bool>
{
    /// <summary>
    /// Creates a point.
    /// </summary>
    /// <param name="x">The X coordinate (row).</param>
    /// <param name="y">The Y coordinate (column).</param>
    public Point(T x, T y) {
        this.X = x;
        this.Y = y;
    }
    
    /// <summary>
    /// Compares the coordinates of two points.
    /// </summary>
    /// <param name="other">The point to compare with.</param>
    /// <returns><c>true</c> if <paramref name="other"/> is not <c>null</c> and has the same coordinates.</returns>
    public bool Equals(Point<T> other) => X == other.X && Y == other.Y;

    /// <summary>The X coordinate; on the board, the row.</summary>
    public T X { get; set; }
    /// <summary>The Y coordinate; on the board, the column.</summary>
    public T Y { get; set; }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);
}