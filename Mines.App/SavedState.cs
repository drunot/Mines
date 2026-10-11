using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mines;

// What is remembered between runs: the last settings, and the board if a game was left unfinished.
// Stored as JSON in the user's settings folder (~/.config/Mines/state.json on Linux,
// %APPDATA%\Mines\state.json on Windows).
public sealed class SavedState
{
    public SavedSettings Settings { get; set; } = new();

    // Only an unfinished game: started (bombs placed), and neither lost nor won.
    public SavedGame? Game { get; set; }

    // Settable so tests can use another file.
    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mines", "state.json");

    // The saved state, or null if there is none or it can't be read (then the game starts with its defaults).
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

    // Saving is best effort: if the file can't be written, the game just starts fresh next time.
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

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class SavedSettings
{
    public int Rows { get; set; } = 15;
    public int Columns { get; set; } = 25;
    public int BombCount { get; set; } = 75;
    public GeneratorKind Generator { get; set; } = GeneratorKind.NoGuessing;
}

// An unfinished board. Cells are Field.Index values (row * Columns + column).
public sealed class SavedGame
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int[] Bombs { get; set; } = [];
    public int[] Revealed { get; set; } = [];
    public int[] Flagged { get; set; } = [];
    public int[] Questioned { get; set; } = [];
    public double ElapsedSeconds { get; set; }

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

    // Guards against a damaged or hand-edited file: sizes in range, cells on the board, no revealed bomb.
    public bool IsValid()
    {
        int cells = Rows * Columns;
        bool OnBoard(int[] list) => list.All(i => i >= 0 && i < cells);
        return Rows > 0 && Columns > 0 && cells <= 1_000_000 && Bombs.Length > 0
            && OnBoard(Bombs) && OnBoard(Revealed) && OnBoard(Flagged) && OnBoard(Questioned)
            && !Revealed.Intersect(Bombs).Any() && ElapsedSeconds >= 0;
    }

    // A board in the saved state: bombs placed, the revealed cells revealed and the flags set.
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
