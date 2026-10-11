namespace Mines.Solving;

interface ISolvingStrategy
{
    bool ApplyStrategy(FieldMatrix fieldMatrix, EquationList equations);
}

