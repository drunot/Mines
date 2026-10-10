using System;
using System.Numerics;

namespace Mines;
public class Point<T> : IEquatable<Point<T>> 
    where T : IEqualityOperators<T, T, bool>
{
    public Point(T x, T y) {
        this.X = x;
        this.Y = y;
    }
    
    public bool Equals(Point<T>? other) => other is not null && X == other.X && Y == other.Y;

    public T X { get; set; }
    public T Y { get; set; }

    public override int GetHashCode() => HashCode.Combine(X, Y);
}