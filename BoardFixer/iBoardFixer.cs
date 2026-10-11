
namespace Mines.BoardFixer;
using System.Diagnostics.CodeAnalysis;

public interface iBoardFixer
{
    bool FixBoard(FieldMatrix board, [NotNullWhen(true)] out bool[,]? fixed_board);
}