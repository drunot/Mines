using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mines;

/// <summary>
/// What is remembered between runs: the last settings, and the board if a game was left unfinished.
/// </summary>
/// <remarks>
/// Stored as JSON in the user's settings folder (~/.config/Mines/state.json on Linux,
/// %APPDATA%\Mines\state.json on Windows).
/// </remarks>
public sealed class SavedState
{
    /// <summary>
    /// The last used settings.
    /// </summary>
    public SavedSettings Settings { get; set; } = new();

    /// <summary>
    /// The game in progress, or null. Only an unfinished game: started (bombs placed), and neither lost nor won.
    /// </summary>
    public SavedGame? Game { get; set; }

    /// <summary>
    /// The path of the state file. Settable so tests can use another file.
    /// </summary>
    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mines", "state.json");

    /// <summary>
    /// Reads the state from <see cref="FilePath"/>.
    /// </summary>
    /// <returns>
    /// The saved state, or null if there is none or it can't be read (then the game starts with its defaults).
    /// </returns>
    public static SavedState? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<SavedState>(File.ReadAllText(FilePath), s_json) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes this state to <see cref="FilePath"/>.
    /// </summary>
    /// <remarks>
    /// Saving is best effort: if the file can't be written, the game just starts fresh next time.
    /// </remarks>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Write a temporary file and then replace the old one, so a crash while saving can't leave half a file.
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, s_json));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// JSON options for the state file: indented, with enums written as names.
    /// </summary>
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>
/// The saved game settings: board size, bomb count and generator.
/// </summary>
public sealed class SavedSettings
{
    /// <summary>
    /// The number of rows.
    /// </summary>
    public int Rows { get; set; } = 15;
    /// <summary>
    /// The number of columns.
    /// </summary>
    public int Columns { get; set; } = 25;
    /// <summary>
    /// The number of bombs.
    /// </summary>
    public int BombCount { get; set; } = 75;
    /// <summary>
    /// Which generator makes new boards.
    /// </summary>
    public GeneratorKind Generator { get; set; } = GeneratorKind.NoGuessing;
}

/// <summary>
/// An unfinished board. Cells are <c>Field.Index</c> values (row * Columns + column).
/// </summary>
public sealed class SavedGame
{
    /// <summary>
    /// The number of rows of the board.
    /// </summary>
    public int Rows { get; set; }
    /// <summary>
    /// The number of columns of the board.
    /// </summary>
    public int Columns { get; set; }
    /// <summary>
    /// The cells that hold a bomb.
    /// </summary>
    public int[] Bombs { get; set; } = [];
    /// <summary>
    /// The revealed cells.
    /// </summary>
    public int[] Revealed { get; set; } = [];
    /// <summary>
    /// The flagged cells.
    /// </summary>
    public int[] Flagged { get; set; } = [];
    /// <summary>
    /// The question-marked cells.
    /// </summary>
    public int[] Questioned { get; set; } = [];
    /// <summary>
    /// The time played so far, in seconds.
    /// </summary>
    public double ElapsedSeconds { get; set; }

    /// <summary>
    /// Captures a board in a <see cref="SavedGame"/>.
    /// </summary>
    /// <param name="board">The board to save.</param>
    /// <param name="questioned">The question-marked cells (<c>Field.Index</c> values).</param>
    /// <param name="elapsed">The time played so far.</param>
    /// <returns>The saved game.</returns>
    public static SavedGame From(FieldMatrix board, System.Collections.Generic.IEnumerable<int> questioned, TimeSpan elapsed)
    {
        var fields = board.Fields.Cast<Field>().ToList();
        return new SavedGame
        {
            Rows = board.Rows,
            Columns = board.Columns,
            Bombs = fields.Where(f => f.IsBomb).Select(f => f.Index).ToArray(),
            Revealed = fields.Where(f => !f.Hidden).Select(f => f.Index).ToArray(),
            Flagged = fields.Where(f => f.Flagged).Select(f => f.Index).ToArray(),
            Questioned = questioned.ToArray(),
            ElapsedSeconds = elapsed.TotalSeconds,
        };
    }

    /// <summary>
    /// Checks that the saved game can be restored.
    /// </summary>
    /// <remarks>
    /// Guards against a damaged or hand-edited file: sizes in range, cells on the board, no revealed bomb.
    /// </remarks>
    /// <returns>True if the saved game is usable.</returns>
    public bool IsValid()
    {
        int cells = Rows * Columns;
        bool OnBoard(int[] list) => list.All(i => i >= 0 && i < cells);
        return Rows > 0 && Columns > 0 && cells <= 1_000_000 && Bombs.Length > 0
            && OnBoard(Bombs) && OnBoard(Revealed) && OnBoard(Flagged) && OnBoard(Questioned)
            && !Revealed.Intersect(Bombs).Any() && ElapsedSeconds >= 0;
    }

    /// <summary>
    /// Restores the board.
    /// </summary>
    /// <returns>A board in the saved state: bombs placed, the revealed cells revealed and the flags set.</returns>
    public FieldMatrix ToBoard()
    {
        var bombs = new bool[Rows, Columns];
        foreach (int i in Bombs)
        {
            bombs[i / Columns, i % Columns] = true;
        }
        var board = new FieldMatrix(bombs);
        foreach (int i in Revealed)
        {
            board.FieldAt(i).Hidden = false;
        }
        foreach (int i in Flagged)
        {
            board.FieldAt(i).Flagged = true;
        }
        return board;
    }
}
