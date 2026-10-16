using System;
using System.Collections.Generic;
using System.Linq;
using Mines.BoardFixer;
using Mines.Generators;
using Mines.Solving;

namespace Mines;

/// <summary>
/// Which cells the first click of an <see cref="InfiniteFieldMatrix"/> keeps free of bombs.
/// </summary>
public enum GeneratorType
{
    /// <summary>The clicked cell and the 8 around it, so the first click always opens an area.</summary>
    Deterministic,
    /// <summary>Only the clicked cell.</summary>
    Random,
}

/// <summary>
/// A board without edges, made of square chunks (<see cref="FieldMatrix"/>es of <c>chunk_size</c> by
/// <c>chunk_size</c> fields) that are created as they are needed. Work in progress.
/// </summary>
/// <remarks>
/// <para>
/// Fields have global positions, which may be negative; each chunk is stored under its chunk row and column.
/// A chunk gets its bombs when it is created, and the 8 chunks around any revealed cell always exist, so every
/// revealed number counts all its neighbours.
/// </para>
/// <para>
/// The game never ends: hitting a bomb resets <see cref="Streak"/>, the number of safe clicks in a row. A
/// cascade only opens cells near the visible area (<see cref="SetVisibleArea"/>); the rest waits until the view
/// moves there, so a nearly empty board can't keep revealing forever.
/// </para>
/// </remarks>
public class InfiniteFieldMatrix : IBoard
{
    /// <summary>
    /// Creates an empty infinite board. Its chunks and bombs are made by the first <see cref="Reveal"/>.
    /// </summary>
    /// <param name="bomb_density">The share of cells that are bombs, e.g. 0.15 for 15%.</param>
    /// <param name="type">Which cells the first click keeps free of bombs.</param>
    /// <param name="random">The random number source; a new <see cref="Random"/> if <c>null</c>.</param>
    public InfiniteFieldMatrix(float bomb_density, GeneratorType type = GeneratorType.Random, Random? random = null)
    {
        _chunk_bomb_count = (int)(chunk_size * chunk_size * bomb_density);
        _rand = random ?? new Random();
        _generatorType = type;
    }

    /// <summary>The number of rows and columns of each chunk.</summary>
    const int chunk_size = 15;

    /// <summary>
    /// The most chunks the cascade may work in at once (40 by 40 chunks, 600 by 600 cells), however far the view
    /// is zoomed out.
    /// </summary>
    const int max_active_side = 40;

    // ---- IBoard ----

    /// <inheritdoc/>
    /// <remarks>Always <c>null</c>: the board has no edges.</remarks>
    public BoardSize? Size => null;

    /// <inheritdoc/>
    public bool Initialized => !_first;

    /// <inheritdoc/>
    public bool GameLost => false;

    /// <inheritdoc/>
    public bool GameWon => false;

    /// <inheritdoc/>
    /// <remarks>Raised once per player action or view move, not once per revealed cell.</remarks>
    public event EventHandler? FieldChanged;

    /// <inheritdoc/>
    /// <remarks>
    /// The clicked cell is on screen, so it is always revealed; only the cascade it starts is limited to the
    /// active area. A click that reveals at least one cell without hitting a bomb adds one to
    /// <see cref="Streak"/>, and a chord adds one for every neighbour it opens that the player would otherwise have
    /// had to click (not counting cells opened by the cascade of another one), so chording never costs streak.
    /// Hitting a bomb resets it to 0.
    /// </remarks>
    public void Reveal(long row, long column)
    {
        long revealed_before = _revealed_count;
        _bomb_hit_in_action = false;

        bool created = EnsureRing(row, column, out _);   // the first click also keeps the start area free
        Field field = FieldAt(row, column)!;
        if (created && field.Hidden && _generatorType == GeneratorType.Deterministic)
        {
            MakeSolvable(field);
        }
        int clicks = 0;                   // the clicks this action is worth for the streak
        if (field.Hidden)
        {
            field.Hidden = false;         // (on a flagged cell this only removes the flag)
            clicks = 1;
        }
        else if (field.Number is int number && number <= field.Neighbors.Count(f => f.Flagged || !f.Hidden && f.IsBomb))
        {
            // Chord: a revealed number with all its bombs flagged or already revealed reveals the other neighbours.
            // Each neighbour it opens counts as the click it saves; a neighbour that the cascade of an earlier one
            // already opened is skipped, like the rest of a cascade.
            foreach (Field neighbor in field.Neighbors)
            {
                if (!neighbor.Flagged && neighbor.Hidden)
                {
                    neighbor.Hidden = false;
                    clicks++;
                }
            }
        }
        ProcessQueue();

        if (_revealed_count > revealed_before)
        {
            LastActionHitBomb = _bomb_hit_in_action;
            if (!_bomb_hit_in_action)
            {
                Streak += clicks;
                BestStreak = Math.Max(BestStreak, Streak);
            }
        }
        FieldChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>Does nothing before the first <see cref="Reveal"/>, which creates the first chunks.</remarks>
    public void CycleMark(long row, long column)
    {
        if (_first)
        {
            return;
        }
        EnsureRing(row, column, out _);
        Field field = FieldAt(row, column)!;
        if (!field.Hidden)
        {
            return;
        }
        FieldMatrix.CycleMark(field);
        FieldChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The active area is the chunks the visible cells are in plus one chunk around them, at most
    /// <see cref="max_active_side"/> chunks each way. Cells that a cascade reached there earlier are revealed
    /// now; that doesn't change <see cref="Streak"/>.
    /// </remarks>
    public void SetVisibleArea(long top, long left, long bottom, long right)
    {
        Point<long> first = GlobalToLocal(top, left).chunk;
        Point<long> last = GlobalToLocal(bottom, right).chunk;
        long first_r = first.X - 1, last_r = last.X + 1;
        long first_c = first.Y - 1, last_c = last.Y + 1;
        (first_r, last_r) = Limit(first_r, last_r);
        (first_c, last_c) = Limit(first_c, last_c);

        var area = (first_r, first_c, last_r, last_c);
        if (area == _active_area)
        {
            return;   // same chunks as before
        }
        _active_area = area;

        var chunks = new List<Point<long>>();
        for (long r = first_r; r <= last_r; ++r)
        {
            for (long c = first_c; c <= last_c; ++c)
            {
                chunks.Add(new Point<long>(r, c));
            }
        }
        SetActiveArea(chunks);
        FieldChanged?.Invoke(this, EventArgs.Empty);

        // At most max_active_side chunks from first to last, keeping the middle.
        static (long, long) Limit(long first, long last)
        {
            long extra = last - first + 1 - max_active_side;
            return extra <= 0 ? (first, last) : (first + extra / 2, first + extra / 2 + max_active_side - 1);
        }
    }

    // ---- Saving and loading ----

    /// <summary>
    /// Captures the board: every chunk with its bombs, revealed cells and marks, and the counters.
    /// </summary>
    /// <returns>The state, to give to <see cref="Load"/> later.</returns>
    public InfiniteBoardState Save()
    {
        var state = new InfiniteBoardState
        {
            Streak = Streak,
            BestStreak = BestStreak,
            BombsHit = BombsHit,
            LastActionHitBomb = LastActionHitBomb,
        };
        var cells = new char[chunk_size * chunk_size];
        foreach (var (position, chunk) in _chunks)
        {
            for (int r = 0; r < chunk_size; ++r)
            {
                for (int c = 0; c < chunk_size; ++c)
                {
                    Field f = chunk[r, c];
                    int code = (f.IsBomb ? 1 : 0) + (f.Hidden ? 0 : 2) + (f.Flagged ? 4 : 0) + (f.Questioned ? 8 : 0);
                    cells[r * chunk_size + c] = (char)('a' + code);
                }
            }
            state.Chunks.Add(new ChunkState
            {
                Row = position.X,
                Column = position.Y,
                Verified = !_unverified.Contains(position),
                Cells = new string(cells),
            });
        }
        return state;
    }

    /// <summary>
    /// Continues a board saved with <see cref="Save"/>.
    /// </summary>
    /// <remarks>
    /// Cells next to a revealed 0 that weren't revealed yet (outside the visible area when saving) are found
    /// again, so the cascade continues when the view gets there.
    /// </remarks>
    /// <param name="state">The saved state.</param>
    /// <param name="bomb_density">The share of cells that are bombs, for chunks created from now on.</param>
    /// <param name="type">Which generator makes new chunks.</param>
    /// <param name="random">The random number source; a new <see cref="Random"/> if <c>null</c>.</param>
    /// <returns>The board, or <c>null</c> if the state is damaged (wrong sizes, unknown letters, a chunk twice).</returns>
    public static InfiniteFieldMatrix? Load(InfiniteBoardState state, float bomb_density, GeneratorType type,
                                            Random? random = null)
    {
        var board = new InfiniteFieldMatrix(bomb_density, type, random);
        board._infiniteFieldSource = new InfiniteFieldOwner(board);
        foreach (ChunkState saved in state.Chunks)
        {
            var position = new Point<long>(saved.Row, saved.Column);
            if (saved.Cells is null || saved.Cells.Length != chunk_size * chunk_size
                || saved.Cells.Any(ch => ch < 'a' || ch > 'p') || board._chunks.ContainsKey(position))
            {
                return null;
            }
            var chunk = new FieldMatrix(chunk_size, saved.Row, saved.Column, board._chunk_bomb_count,
                                        board._infiniteFieldSource);
            var bombs = new bool[chunk_size, chunk_size];
            for (int i = 0; i < saved.Cells.Length; ++i)
            {
                bombs[i / chunk_size, i % chunk_size] = ((saved.Cells[i] - 'a') & 1) != 0;
            }
            chunk.ApplyBombs(bombs);
            for (int i = 0; i < saved.Cells.Length; ++i)
            {
                int code = saved.Cells[i] - 'a';
                Field f = chunk[i / chunk_size, i % chunk_size];
                if ((code & 2) != 0)
                {
                    f.SetHiddenNoPropagate(false);   // no cascade or counting: it was revealed before
                }
                else
                {
                    f.Mark = (code & 4) != 0 ? Mark.Flag : (code & 8) != 0 ? Mark.Question : Mark.None;
                }
            }
            board._chunks[position] = chunk;
            if (!saved.Verified)
            {
                board._unverified.Add(position);
            }
        }

        board._first = board._chunks.Count == 0;
        board.Streak = state.Streak;
        board.BestStreak = state.BestStreak;
        board.BombsHit = state.BombsHit;
        board.LastActionHitBomb = state.LastActionHitBomb;

        // Every revealed cell needs its ring (a saved board has them); then find the cascade's unfinished work.
        var revealed = board._chunks.Values.SelectMany(chunk => chunk.Fields.Cast<Field>()).Where(f => !f.Hidden).ToList();
        foreach (Field f in revealed)
        {
            board.EnsureRing(f.Position.X, f.Position.Y, out _);
        }
        foreach (Field f in revealed)
        {
            if (f.IsBomb || f.Number is not null)
            {
                continue;
            }
            foreach (Field n in f.Neighbors)
            {
                if (n.Hidden && !n.Flagged)
                {
                    board.Pending(board.GlobalToLocal(n.Position.X, n.Position.Y).chunk).Add(n);
                }
            }
        }
        return board;
    }

    // ---- Counters ----

    /// <summary>
    /// Safe clicks in a row: player actions that revealed something without hitting a bomb. A chord counts as the
    /// clicks it saves.
    /// </summary>
    public int Streak { get; private set; }

    /// <summary>The longest <see cref="Streak"/> so far.</summary>
    public int BestStreak { get; private set; }

    /// <summary>How many bombs have been hit.</summary>
    public int BombsHit { get; private set; }

    /// <summary>Whether the last action that revealed anything hit a bomb (for the face in the toolbar).</summary>
    public bool LastActionHitBomb { get; private set; }

    /// <summary>Every revealed cell counts once; lets <see cref="Reveal"/> see whether it revealed anything.</summary>
    long _revealed_count;

    /// <summary>Set when a bomb is revealed during the current <see cref="Reveal"/>.</summary>
    bool _bomb_hit_in_action;

    // ---- The cascade: a queue limited to the active area ----

    /// <summary>Cells to reveal: the neighbours of revealed cells without neighbouring bombs.</summary>
    Queue<Field> _reveal_queue = new();

    /// <summary>
    /// Cells next to a revealed 0 that are waiting to be revealed, by chunk: they lie outside the active area.
    /// </summary>
    private readonly Dictionary<Point<long>, HashSet<Field>> _pending = new();

    /// <summary>The chunks the cascade may enter now.</summary>
    HashSet<Point<long>> _active = new();

    /// <summary>The chunk rows and columns of <see cref="_active"/>, to skip unchanged areas.</summary>
    (long, long, long, long) _active_area = (0, 0, -1, -1);

    /// <summary>True while <see cref="ProcessQueue"/> runs, so a reveal inside it doesn't start another one.</summary>
    bool _processing = false;

    /// <summary>
    /// The pending cells of <paramref name="chunk"/>, creating the set on first use.
    /// </summary>
    /// <param name="chunk">The chunk's row (X) and column (Y) among the chunks.</param>
    /// <returns>The chunk's set of pending cells.</returns>
    private HashSet<Field> Pending(Point<long> chunk)
    {
        if (!_pending.TryGetValue(chunk, out HashSet<Field>? cells))
        {
            cells = new HashSet<Field>();
            _pending[chunk] = cells;
        }
        return cells;
    }

    // ---- No guessing: arranging new chunks so the area around a revealed cell can be solved ----

    /// <summary>
    /// Solves windows. It knows only what the player could: no total bomb count, and no numbers from cells on
    /// the window's edge (they have neighbours outside it).
    /// </summary>
    IResumableSolver _window_solver = new ConstraintSolver { UseGlobalEquation = false, IgnoreEdges = true };

    /// <summary>Moves bombs between unlocked cells when the window solver gets stuck.</summary>
    iBoardFixer _boardFixer = new EdgeBoardFixer();

    /// <summary>How many fixes are tried on one layout before reshuffling.</summary>
    const int max_fixes = 40;

    /// <summary>How many times the free cells are reshuffled before giving up.</summary>
    const int max_shuffles = 50;

    /// <summary>
    /// Chunks that have never been part of a solved window. Only their unlocked cells are reshuffled, so
    /// layouts that earlier windows found solvable stay as they are.
    /// </summary>
    readonly HashSet<Point<long>> _unverified = new();

    /// <summary>
    /// Arranges the bombs around <paramref name="trigger"/>, which is about to be revealed, so that the player can
    /// solve its whole chunk by logic from what they see now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Works on a window of the 3 by 3 chunks around the trigger's chunk. Cells the player can see (revealed, or
    /// next to a revealed cell, the trigger included) are locked and never change. The window is solved from the
    /// player's view; if the centre chunk can't be finished, the board fixer moves bombs and solving continues
    /// (<see cref="TryFix"/>). If that fails too, the free cells of new chunks near the stuck area are reshuffled
    /// and it starts again.
    /// </para>
    /// <para>
    /// The window's outer chunks don't have to be solvable yet: they border chunks that don't exist. They are
    /// handled when they become the centre of a later window.
    /// </para>
    /// <para>
    /// This is a best effort: after <see cref="max_shuffles"/> reshuffles without success the last layout is
    /// kept, and that spot may need a guess. It can't always succeed, because cells the player has seen can't
    /// change, and blind clicks or hit bombs can leave a situation that no move of the other bombs resolves.
    /// </para>
    /// </remarks>
    /// <param name="trigger">The cell about to be revealed; the ring of chunks around it must exist.</param>
    void MakeSolvable(Field trigger)
    {
        Point<long> chunk = GlobalToLocal(trigger.Position.X, trigger.Position.Y).chunk;
        FieldMatrix window = CreateWindow(chunk.X, chunk.Y, out bool[,] locked);

        // The trigger is about to be revealed: the player will see it and its number.
        int tr = (int)(trigger.Position.X - (chunk.X - 1) * chunk_size);
        int tc = (int)(trigger.Position.Y - (chunk.Y - 1) * chunk_size);
        bool[,] player_hidden = window.GetHidden();
        player_hidden[tr, tc] = false;
        for (int r = Math.Max(0, tr - 1); r <= Math.Min(window.Rows - 1, tr + 1); ++r)
        {
            for (int c = Math.Max(0, tc - 1); c <= Math.Min(window.Columns - 1, tc + 1); ++c)
            {
                locked[r, c] = true;
            }
        }
        bool[,] shuffleable = Shuffleable(chunk, locked);

        for (int shuffle = 0; ; ++shuffle)
        {
            if (TryFix(window, player_hidden, locked, out FieldMatrix reached))
            {
                break;
            }
            if (shuffle == max_shuffles || !ShuffleUnlocked(window, shuffleable, reached))
            {
                // No solvable layout was found: the last one is kept, so the player may need a guess here. That is
                // why the game calls this "attempts no guessing" rather than promising it.
                break;
            }
        }

        WindowToFields(window, chunk.X, chunk.Y);
        for (long r = chunk.X - 1; r <= chunk.X + 1; ++r)
        {
            for (long c = chunk.Y - 1; c <= chunk.Y + 1; ++c)
            {
                _unverified.Remove(new Point<long>(r, c));
            }
        }
    }

    /// <summary>
    /// Tries to make the centre chunk of the window solvable from the player's view by moving bombs, without
    /// touching locked cells.
    /// </summary>
    /// <remarks>
    /// Solves a copy of the window from the player's view. While the centre isn't finished, the fixer moves bombs
    /// and solving continues from there (fast, but deductions made before a swap may rely on numbers it changed).
    /// So once the centre is finished, a fresh copy with the new layout is solved from the player's view again;
    /// only that counts. If it gets stuck, fixing continues from the fresh copy, which the player can reach.
    /// </remarks>
    /// <param name="window">The layout to change; its bombs are updated with every fix.</param>
    /// <param name="player_hidden">Which window cells the player has not revealed.</param>
    /// <param name="locked">Cells whose bombs must not move.</param>
    /// <param name="reached">The last solved copy: where the solver got stuck, if it failed.</param>
    /// <returns><c>true</c> if the centre chunk can be solved from the player's view.</returns>
    bool TryFix(FieldMatrix window, bool[,] player_hidden, bool[,] locked, out FieldMatrix reached)
    {
        reached = new FieldMatrix(window.GetBombs(), player_hidden);
        _window_solver.Solve(reached);
        for (int i = 0; i < max_fixes; ++i)
        {
            if (CentreSolved(reached))
            {
                var fresh = new FieldMatrix(window.GetBombs(), player_hidden);
                _window_solver.Solve(fresh);
                reached = fresh;   // the solver now works on the fresh copy
                if (CentreSolved(fresh))
                {
                    return true;
                }
            }
            if (!_boardFixer.GetBoardChanges(reached, out var swaps, locked))
            {
                return false;
            }
            foreach (var swap in swaps)
            {
                window.SwapBombs(swap.Item1, swap.Item2);
            }
            _window_solver.SwapAndContinue(swaps);   // swaps the same bombs in `reached` and solves on
        }
        return false;
    }

    /// <summary>
    /// Whether every safe cell of the window's centre chunk is revealed.
    /// </summary>
    /// <param name="window">A solved copy of a window.</param>
    /// <returns><c>true</c> if the centre chunk is finished.</returns>
    static bool CentreSolved(FieldMatrix window)
    {
        for (int r = chunk_size; r < 2 * chunk_size; ++r)
        {
            for (int c = chunk_size; c < 2 * chunk_size; ++c)
            {
                if (window[r, c].Hidden && !window[r, c].IsBomb)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// The window cells a reshuffle may change: unlocked, in a chunk that has never been part of a solved window.
    /// </summary>
    /// <param name="chunk">The window's centre chunk.</param>
    /// <param name="locked">The window's lock mask.</param>
    /// <returns><c>[row, column]</c> is true for a cell that may be reshuffled.</returns>
    bool[,] Shuffleable(Point<long> chunk, bool[,] locked)
    {
        var result = new bool[3 * chunk_size, 3 * chunk_size];
        for (int c_r = 0; c_r < 3; ++c_r)
        {
            for (int c_c = 0; c_c < 3; ++c_c)
            {
                if (!_unverified.Contains(new Point<long>(chunk.X - 1 + c_r, chunk.Y - 1 + c_c)))
                {
                    continue;
                }
                for (int l_r = 0; l_r < chunk_size; ++l_r)
                {
                    for (int l_c = 0; l_c < chunk_size; ++l_c)
                    {
                        int r = c_r * chunk_size + l_r, c = c_c * chunk_size + l_c;
                        result[r, c] = !locked[r, c];
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Reveals the queued cells that lie in the active area, and parks the others in <see cref="_pending"/>.
    /// Revealing a cell without neighbouring bombs queues its neighbours (in <see cref="InfiniteFieldOwner"/>).
    /// </summary>
    void ProcessQueue()
    {
        if (_processing)
        {
            return;   // a running ProcessQueue further up the call stack will handle the queue
        }
        _processing = true;
        try
        {
            while (_reveal_queue.TryDequeue(out Field? field))
            {
                if (!field.Hidden || field.Flagged)
                {
                    continue;
                }
                Point<long> chunk = GlobalToLocal(field.Position.X, field.Position.Y).chunk;
                if (!_active.Contains(chunk))
                {
                    Pending(chunk).Add(field);          // later, when the area moves here
                    continue;
                }
                if (EnsureRing(field.Position.X, field.Position.Y, out _) && _generatorType == GeneratorType.Deterministic)
                {
                    MakeSolvable(field);   // new chunks appeared: arrange them so this area can be solved
                }
                field.Hidden = false;
            }
        }
        finally
        {
            _processing = false;
        }
    }

    /// <summary>
    /// Places the bombs of some reshufflable cells at random among those cells again, keeping their number.
    /// Prefers the cells near where the solver got stuck, so the parts that could be solved keep their layout.
    /// </summary>
    /// <remarks>
    /// Uses the reshufflable cells within 2 cells of a hidden, undecided cell of the centre chunk. If there are
    /// fewer than 9 of those, all reshufflable cells are used instead.
    /// </remarks>
    /// <param name="window">The layout to change.</param>
    /// <param name="shuffleable">The cells that may change (<see cref="Shuffleable"/>).</param>
    /// <param name="reached">The solved copy where the solver got stuck.</param>
    /// <returns><c>false</c> if there was nothing to reshuffle.</returns>
    bool ShuffleUnlocked(FieldMatrix window, bool[,] shuffleable, FieldMatrix reached)
    {
        // Cells near the undecided cells of the centre chunk.
        var near = new bool[window.Rows, window.Columns];
        for (int r = chunk_size; r < 2 * chunk_size; ++r)
        {
            for (int c = chunk_size; c < 2 * chunk_size; ++c)
            {
                if (!reached[r, c].Hidden || reached[r, c].Flagged)
                {
                    continue;   // decided
                }
                for (int nr = r - 2; nr <= r + 2; ++nr)
                {
                    for (int nc = c - 2; nc <= c + 2; ++nc)
                    {
                        near[nr, nc] = true;   // within the window: the centre is chunk_size away from its edges
                    }
                }
            }
        }

        var cells = new List<Point<int>>();
        for (int r = 0; r < window.Rows; ++r)
        {
            for (int c = 0; c < window.Columns; ++c)
            {
                if (shuffleable[r, c] && near[r, c])
                {
                    cells.Add(new Point<int>(r, c));
                }
            }
        }
        if (cells.Count < 9)
        {
            cells.Clear();
            for (int r = 0; r < window.Rows; ++r)
            {
                for (int c = 0; c < window.Columns; ++c)
                {
                    if (shuffleable[r, c])
                    {
                        cells.Add(new Point<int>(r, c));
                    }
                }
            }
        }
        if (cells.Count == 0)
        {
            return false;
        }

        bool[,] bombs = window.GetBombs();
        int bomb_count = cells.Count(p => bombs[p.X, p.Y]);
        foreach (Point<int> p in cells)
        {
            bombs[p.X, p.Y] = false;
        }
        Point<int>[] order = cells.ToArray();
        _rand.Shuffle(order);
        for (int i = 0; i < bomb_count; ++i)
        {
            bombs[order[i].X, order[i].Y] = true;
        }
        window.ApplyBombs(bombs);
        return true;
    }

    /// <summary>
    /// Makes <paramref name="chunks"/> the active area and continues the cascade into the chunks that became
    /// active.
    /// </summary>
    /// <param name="chunks">The chunks the cascade may enter.</param>
    void SetActiveArea(IEnumerable<Point<long>> chunks)
    {
        _active = chunks.ToHashSet();
        foreach (Point<long> chunk in _active)
        {
            if (_pending.Remove(chunk, out HashSet<Field>? waiting))
            {
                foreach (Field f in waiting)
                {
                    _reveal_queue.Enqueue(f);
                }
            }
        }
        ProcessQueue();
    }

    /// <summary>
    /// The owner of every field of every chunk: finds fields by global position in the chunks, keeps the
    /// counters and runs the cascade through the queue.
    /// </summary>
    class InfiniteFieldOwner : IFieldOwner
    {
        /// <summary>
        /// Creates the owner for <paramref name="ifm"/>.
        /// </summary>
        /// <param name="ifm">The infinite board the fields belong to.</param>
        public InfiniteFieldOwner(InfiniteFieldMatrix ifm)
        {
            _ifm = ifm;
        }

        /// <summary>The infinite board the fields belong to.</summary>
        InfiniteFieldMatrix _ifm;

        /// <inheritdoc/>
        public Field? FieldAt(long r, long c)
        {
            return _ifm.FieldAt(r, c);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Makes sure the chunks around the cell exist before its number is worked out. A bomb resets the
        /// streak; a cell without neighbouring bombs queues its neighbours.
        /// </remarks>
        public void OnRevealed(Field field)
        {
            if (field.Hidden)
            {
                return;   // hidden again: nothing to do
            }
            _ifm.EnsureRing(field.Position.X, field.Position.Y, out _);
            _ifm._revealed_count++;
            if (field.IsBomb)
            {
                _ifm._bomb_hit_in_action = true;
                _ifm.BombsHit++;
                _ifm.Streak = 0;
                return;   // the game goes on
            }
            if (field.Number is not null)
            {
                return;   // a number: the cascade stops here
            }
            foreach (Field n in field.Neighbors)
            {
                if (n.Hidden && !n.Flagged)
                {
                    _ifm._reveal_queue.Enqueue(n);
                }
            }
            _ifm.ProcessQueue();
        }

        /// <inheritdoc/>
        /// <remarks>Nothing to count: an infinite board has no "bombs left".</remarks>
        public void OnFlagChanged(Field field)
        {
        }
    }

    /// <summary>True until the first <see cref="EnsureRing"/>, which creates the chunks of the first click.</summary>
    bool _first = true;

    /// <summary>Which cells the first click keeps free of bombs.</summary>
    GeneratorType _generatorType;

    /// <summary>The owner shared by all chunks; created by the first <see cref="CreateChunk"/>.</summary>
    InfiniteFieldOwner? _infiniteFieldSource;

    /// <summary>
    /// Creates the chunk at chunk row <paramref name="chunk_r"/> and column <paramref name="chunk_c"/>, with
    /// the shared field source and generator. It isn't added to <see cref="_chunks"/>.
    /// </summary>
    /// <param name="chunk_r">The chunk's row among the chunks; may be negative.</param>
    /// <param name="chunk_c">The chunk's column among the chunks; may be negative.</param>
    /// <returns>The new chunk, without bombs yet.</returns>
    void CreateChunk(long chunk_r, long chunk_c)
    {
        if (_infiniteFieldSource is null)
        {
            _infiniteFieldSource = new InfiniteFieldOwner(this);
        }

        var field = new FieldMatrix(chunk_size, chunk_r, chunk_c, _chunk_bomb_count, _infiniteFieldSource);
        PropagateFieldMatrix(field, _chunk_bomb_count);

        _chunks[new Point<long>(chunk_r, chunk_c)] = field;
        _unverified.Add(new Point<long>(chunk_r, chunk_c));
    }

    void CreateFirstChunks(long chunk_r, long chunk_c, Point<long>[] restriction_points)
    {
        var local_points = restriction_points.Select((p) => GlobalToLocal(p.X, p.Y)).Where((p) => p.chunk.X == chunk_r && p.chunk.Y == chunk_c).Select((p) => p.local).ToArray();
        if (_infiniteFieldSource is null)
        {
            _infiniteFieldSource = new InfiniteFieldOwner(this);
        }

        var field = new FieldMatrix(chunk_size, chunk_r, chunk_c, _chunk_bomb_count, _infiniteFieldSource);
        PropagateFieldMatrix(field, _chunk_bomb_count, local_points);
        _chunks[new Point<long>(chunk_r, chunk_c)] = field;
        _unverified.Add(new Point<long>(chunk_r, chunk_c));
    }

    /// <summary>The chunks created so far, by chunk row (X) and column (Y).</summary>
    Dictionary<Point<long>, FieldMatrix> _chunks = new Dictionary<Point<long>, FieldMatrix>();
    private int _chunk_bomb_count;

    (Point<long> chunk, Point<int> local) GlobalToLocal(long r, long c)
    {
        long chunk_r = r >= 0 ? r / chunk_size : (r + 1) / chunk_size - 1;   // floor division
        int local_r = (int)(((r % chunk_size) + chunk_size) % chunk_size);   // always 0..chunk_size-1
        long chunk_c = c >= 0 ? c / chunk_size : (c + 1) / chunk_size - 1;   // floor division
        int local_c = (int)(((c % chunk_size) + chunk_size) % chunk_size);   // always 0..chunk_size-1
        return (new Point<long>(chunk_r, chunk_c), new Point<int>(local_r, local_c));

    }



    /// <inheritdoc/>
    /// <remarks>
    /// Only looks: it never creates a chunk. Splits the position into a chunk and a position inside it with floor
    /// division, so negative positions land in the chunks before 0.
    /// </remarks>
    public Field? FieldAt(long r, long c)
    {
        var points = GlobalToLocal(r, c);

        var location = new Point<long>(points.chunk.X, points.chunk.Y);
        if (_chunks.ContainsKey(location))
        {
            return _chunks[location][points.local.X, points.local.Y];
        }
        return null;
    }

    /// <summary>
    /// Creates the chunk at chunk row <paramref name="chunk_r"/> and column <paramref name="chunk_c"/>, with its
    /// bombs, unless it exists already.
    /// </summary>
    /// <param name="chunk_r">The chunk's row among the chunks; may be negative.</param>
    /// <param name="chunk_c">The chunk's column among the chunks; may be negative.</param>
    /// <returns><c>true</c> if the chunk was created now.</returns>
    public bool CreateChunkIfNotExists(long chunk_r, long chunk_c)
    {
        bool created = false;
        var point = new Point<long>(chunk_r, chunk_c);
        if (!_chunks.ContainsKey(point))
        {
            CreateChunk(chunk_r, chunk_c);

            created = true;
        }
        return created;
    }

    /// <summary>
    /// Makes sure the chunk of a cell and the 8 chunks around it exist, so every neighbour of every cell in that
    /// chunk exists. The first call creates the chunks of the first click, keeping its start area free of bombs.
    /// </summary>
    /// <param name="r">The cell's global row.</param>
    /// <param name="c">The cell's global column.</param>
    /// <param name="center_chunk">The chunk the cell is in.</param>
    /// <returns><c>true</c> if any chunk was created.</returns>
    public bool EnsureRing(long r, long c, out FieldMatrix center_chunk)
    {
        bool new_created = _first;

        var points = GlobalToLocal(r, c);

        var restrictionWindow = _first ? GetRestrictionWindow(new Point<long>(r, c)) : [];

        for (long rIdx = points.chunk.X - 1; rIdx <= points.chunk.X + 1; ++rIdx)
        {
            for (long cIdx = points.chunk.Y - 1; cIdx <= points.chunk.Y + 1; ++cIdx)
            {
                if (_first)
                {
                    CreateFirstChunks(rIdx, cIdx, restrictionWindow);
                }
                else
                {
                    new_created |= CreateChunkIfNotExists(rIdx, cIdx);
                }
            }
        }
        _first = false;
        center_chunk = _chunks[new Point<long>(points.chunk.X, points.chunk.Y)];
        return new_created;
    }

    /// <summary>
    /// Whether moving the bomb of <paramref name="field"/> would change something the player can see: it is
    /// revealed, or next to a revealed cell. Looks the neighbours up without caching them, so it is safe for cells
    /// whose surrounding chunks don't all exist yet.
    /// </summary>
    bool IsLocked(Field field)
    {
        if (!field.Hidden)
        {
            return true;
        }
        for (long r = field.Position.X - 1; r <= field.Position.X + 1; r++)
        {
            for (long c = field.Position.Y - 1; c <= field.Position.Y + 1; c++)
            {
                if (FieldAt(r, c) is Field n && !n.Hidden)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Copies the 3 by 3 chunks around chunk (<paramref name="chunk_r"/>, <paramref name="chunk_c"/>) into one
    /// finite board (bombs and the player's revealed cells), for the solver.
    /// </summary>
    /// <param name="chunk_r">The centre chunk's row among the chunks.</param>
    /// <param name="chunk_c">The centre chunk's column among the chunks.</param>
    /// <param name="locked">All the fields that cannot be moved. They are visible by the user.</param>
    /// <returns>The window: 3 * chunk_size rows and columns.</returns>
    public FieldMatrix CreateWindow(long chunk_r, long chunk_c, out bool[,] locked)
    {
        bool[,] bombs = new bool[3 * chunk_size, 3 * chunk_size];
        bool[,] hidden = new bool[3 * chunk_size, 3 * chunk_size];
        locked = new bool[3 * chunk_size, 3 * chunk_size];
        for (int c_r = 0; c_r < 3; ++c_r)
        {
            for (int c_c = 0; c_c < 3; ++c_c)
            {
                long chunk_idx_r = c_r + chunk_r - 1;
                long chunk_idx_c = c_c + chunk_c - 1;
                var idx = new Point<long>(chunk_idx_r, chunk_idx_c);
                for (int l_r = 0; l_r < chunk_size; ++l_r)
                {
                    for (int l_c = 0; l_c < chunk_size; ++l_c)
                    {
                        var field = _chunks[idx].Fields[l_r, l_c];
                        bombs[c_r * chunk_size + l_r, c_c * chunk_size + l_c] = field.IsBomb;
                        hidden[c_r * chunk_size + l_r, c_c * chunk_size + l_c] = field.Hidden;
                        locked[c_r * chunk_size + l_r, c_c * chunk_size + l_c] = IsLocked(field);
                    }
                }
            }
        }
        return new FieldMatrix(bombs, hidden);
    }

    /// <summary>
    /// Copies the bombs of a window made by <see cref="CreateWindow"/> back into its 9 chunks.
    /// </summary>
    /// <param name="fieldMatrix">The window.</param>
    /// <param name="chunk_r">The centre chunk's row among the chunks.</param>
    /// <param name="chunk_c">The centre chunk's column among the chunks.</param>
    /// <returns>An empty array (to be removed).</returns>
    /// <exception cref="ArgumentException">The window isn't 3 by 3 chunks.</exception>
    public FieldMatrix[] WindowToFields(FieldMatrix fieldMatrix, long chunk_r, long chunk_c)
    {
        if (fieldMatrix.Rows / chunk_size != 3 || fieldMatrix.Columns / chunk_size != 3)
        {
            throw new ArgumentException("Window needs to be exactly 9 chunks");
        }
        FieldMatrix[] matrices = new FieldMatrix[9];


        for (int c_r = 0; c_r < 3; ++c_r)
        {
            for (int c_c = 0; c_c < 3; ++c_c)
            {

                long chunk_idx_r = c_r + chunk_r - 1;
                long chunk_idx_c = c_c + chunk_c - 1;
                bool[,] bombs = new bool[chunk_size, chunk_size];
                for (int l_r = 0; l_r < chunk_size; ++l_r)
                {
                    for (int l_c = 0; l_c < chunk_size; ++l_c)
                    {
                        var field = fieldMatrix.Fields[c_r * chunk_size + l_r, c_c * chunk_size + l_c];
                        bombs[l_r, l_c] = field.IsBomb;
                    }
                }
                _chunks[new Point<long>(chunk_idx_r, chunk_idx_c)].ApplyBombs(bombs);
            }
        }
        return matrices;
    }

    Random _rand;

    Point<long>[] GetRestrictionWindow(Point<long> initial)
    {
        switch (_generatorType)
        {
            case GeneratorType.Random:
                {
                    var point = new Point<long>[1];
                    point[0] = initial;
                    return point;
                }
            case GeneratorType.Deterministic:
                {
                    var point = new Point<long>[9];
                    int idx = 0;
                    for (long x = initial.X - 1; x <= initial.X + 1; ++x)
                    {
                        for (long y = initial.Y - 1; y <= initial.Y + 1; ++y)
                        {
                            point[idx++] = new Point<long>(x, y);
                        }
                    }
                    return point;
                }
        }
        return [];
    }

    void PropagateFieldMatrix(FieldMatrix matrix, int bomb_count, Point<int>[]? restrict = null)
    {
        var bombs = new bool[matrix.Rows, matrix.Columns];

        int[] cells = Enumerable.Range(0, matrix.Rows * matrix.Columns).ToArray();
        _rand.Shuffle(cells);
        int placed = 0;
        for (int j = 0; j < cells.Length && placed < bomb_count; ++j)
        {
            var point = new Point<int>(cells[j] / matrix.Columns, cells[j] % matrix.Columns);
            if (restrict?.Any((p) => p.Equals(point)) == true)
            {
                continue;   // keep the start point free. (Original Window 2000 Minesweeper moves the first bomb if it is where the user clicks.)
            }
            bombs[point.X, point.Y] = true;
            ++placed;
        }
        if (placed < bomb_count)
        {
            throw new ArgumentException("Not enough cells outside the start area for that many bombs.");
        }

        matrix.ApplyBombs(bombs);
    }
}