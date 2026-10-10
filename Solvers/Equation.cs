using System.Collections.Generic;
using System;

namespace Mines.Solving;

public class Equation : IEquatable<Equation>
{

    public Equation()
    {
        sum = 0;
        variables = new HashSet<Point<int>>();
    }

    public Equation(Equation equation)
    {
        sum = equation.sum;
        variables = [.. equation.variables];
    }

    public HashSet<Point<int>> variables;
    public int sum;

    public bool Equals(Equation? other) => sum == other?.sum && variables.SetEquals(other.variables);
    public override bool Equals(object? other) => other is Equation equation && Equals(equation);
    public override int GetHashCode()
    {
        int variables_hash = 0;
        foreach (Point<int> point in variables)
        {
            variables_hash = unchecked(variables_hash + point.GetHashCode());
        }
        return HashCode.Combine(sum, variables_hash);
    }

    public static bool operator ==(Equation left, Equation right) => left.Equals(right);
    public static bool operator !=(Equation left, Equation right) => !left.Equals(right);
}