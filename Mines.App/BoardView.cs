using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Mines;

/// <summary>
/// The whole board in one control. Only the visible cells are drawn, so the cost depends on the window
/// size, not the board size.
/// </summary>
/// <remarks>
/// Mouse wheel zooms around the pointer; dragging (left or middle button) pans.
/// Left click reveals (and chords on a revealed number), right click cycles flag -&gt; question mark -&gt; nothing.
/// Inside a ScrollViewer it shows scrollbars (<see cref="ILogicalScrollable"/>), while still drawing only the
/// visible cells.
/// </remarks>
public class BoardView : Control, ILogicalScrollable
{
    /// <summary>
    /// Defines the <see cref="Board"/> property.
    /// </summary>
    public static readonly StyledProperty<FieldMatrix?> BoardProperty =
        AvaloniaProperty.Register<BoardView, FieldMatrix?>(nameof(Board));

    /// <summary>
    /// The board shown. Setting a new board clears the question marks and resets the view to fit the board.
    /// </summary>
    public FieldMatrix? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="IsLocked"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<BoardView, bool>(nameof(IsLocked));

    /// <summary>
    /// A finished game: clicks no longer reveal or mark cells and cells show no hover or press look.
    /// Zooming and panning still work, to look at the result.
    /// </summary>
    public bool IsLocked
    {
        get => GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    /// <summary>
    /// Makes the control clip to its bounds by default.
    /// </summary>
    static BoardView()
    {
        ClipToBoundsProperty.OverrideDefaultValue<BoardView>(true);
    }

    /// <summary>
    /// Creates the view; it redraws with new colours when the theme variant changes.
    /// </summary>
    public BoardView()
    {
        ActualThemeVariantChanged += (sender, e) =>
        {
            _palette = null;   // colours come from the theme; look them up again
            InvalidateVisual();
        };
    }

    // ---- Layout, in board units (zoom 1). Matches the old 32 px button tiles with a 1 px gap. ----

    /// <summary>
    /// Distance from one cell to the next: a tile plus the gap between tiles.
    /// </summary>
    private const double CellPitch = 33;
    /// <summary>
    /// Width and height of one tile.
    /// </summary>
    private const double TileSize = TileShapes.TileSize;
    /// <summary>
    /// Width of a tile's bevel.
    /// </summary>
    private const double BevelWidth = TileShapes.BevelWidth;
    private const double MaxZoom = 4;        // zooming in stops here (or at the fit, if that is larger)
    private const double DragThreshold = 4;  // pixels the pointer may move before a press becomes a pan

    /// <summary>
    /// Screen pixels per board unit.
    /// </summary>
    private double _zoom = 1;
    private bool _fitted = true;             // at the fit zoom; stays fitted when the size changes
    private Vector _offset;                  // board point shown at the top-left of the visible area

    // ---- Board ----

    /// <summary>
    /// Reacts to <see cref="IsLocked"/> (drops the hover and press state when locking) and to
    /// <see cref="Board"/> (follows the new board's changes, clears the question marks and resets the view).
    /// </summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLockedProperty && IsLocked)
        {
            _hover_cell = null;
            _pressed_cell = null;
            SetPressing(false);
            InvalidateVisual();
        }
        if (change.Property == BoardProperty)
        {
            if (change.OldValue is FieldMatrix old_board)
            {
                old_board.FieldChanged -= OnBoardChanged;
            }
            if (change.NewValue is FieldMatrix new_board)
            {
                new_board.FieldChanged += OnBoardChanged;
            }
            _questioned.Clear();
            _offset = default;
            _fitted = true;
            RaiseScrollInvalidated(EventArgs.Empty);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Redraws when a cell of the board changes.
    /// </summary>
    /// <param name="sender">The board.</param>
    /// <param name="e">The event data.</param>
    private void OnBoardChanged(object? sender, EventArgs e) => InvalidateVisual();

    private TilePalette? _palette;   // colours for the current theme

    /// <summary>
    /// The question-marked cells (<c>Field.Index</c> values).
    /// Question marks are only a reminder for the player, so they live here, not in the game logic.
    /// </summary>
    private readonly HashSet<int> _questioned = new();

    /// <summary>
    /// The question-marked cells (<c>Field.Index</c>), e.g. to save them; <see cref="SetQuestionMarks"/> restores them.
    /// </summary>
    public IReadOnlyCollection<int> QuestionMarks => _questioned;

    /// <summary>
    /// Replaces the question marks, e.g. when a saved game is restored.
    /// </summary>
    /// <param name="cells">The cells to question-mark (<c>Field.Index</c> values).</param>
    public void SetQuestionMarks(IEnumerable<int> cells)
    {
        _questioned.Clear();
        _questioned.UnionWith(cells);
        InvalidateVisual();
    }

    // ---- Size and coordinates ----

    /// <summary>
    /// The size of the whole board in board units (no gap after the last row and column), or empty without a board.
    /// </summary>
    private Size BoardSize => Board is null
        ? default
        : new Size(Board.Columns * CellPitch - (CellPitch - TileSize), Board.Rows * CellPitch - (CellPitch - TileSize));

    /// <summary>
    /// The zoom at which the whole board just fits; zooming out stops here.
    /// </summary>
    /// <param name="bounds">The size of the visible area, in screen pixels.</param>
    /// <returns>The fit zoom, or 1 if the board or the area is empty.</returns>
    private double FitZoom(Size bounds)
    {
        if (BoardSize.Width <= 0 || BoardSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return 1;
        }
        return Math.Min(bounds.Width / BoardSize.Width, bounds.Height / BoardSize.Height);
    }

    /// <summary>
    /// Where the board's top-left corner is drawn, in screen pixels: centred along a direction in which it is
    /// smaller than the view.
    /// </summary>
    private Vector BoardOrigin => new Vector(
        Math.Max(0, (Bounds.Width - BoardSize.Width * _zoom) / 2),
        Math.Max(0, (Bounds.Height - BoardSize.Height * _zoom) / 2));

    /// <summary>
    /// The view fills the space it is given (the ScrollViewer's visible area). Where the space is unbounded,
    /// it asks for the board's size at the current zoom.
    /// </summary>
    /// <param name="availableSize">The space available.</param>
    /// <returns>The desired size.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        Size full = BoardSize * _zoom;
        return new Size(double.IsInfinity(availableSize.Width) ? full.Width : availableSize.Width,
                        double.IsInfinity(availableSize.Height) ? full.Height : availableSize.Height);
    }

    /// <summary>
    /// Records the viewport size, keeps the zoom at least at the fit zoom (and at it while fitted) and
    /// keeps the offset in range.
    /// </summary>
    /// <param name="finalSize">The size given to the view.</param>
    /// <returns>The size used.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size size = base.ArrangeOverride(finalSize);
        _viewport = size;   // Bounds only changes after arranging, but the ScrollViewer asks during it
        double fit = FitZoom(size);
        if (_fitted || _zoom < fit)
        {
            _zoom = fit;   // start fitted, stay fitted on resize, and never be smaller than the view
        }
        ClampOffset(size);
        return size;
    }

    /// <summary>
    /// Keeps the board from being dragged out of view.
    /// </summary>
    /// <param name="bounds">The size of the visible area, in screen pixels.</param>
    /// <param name="notify_scroll_viewer">Whether to raise <see cref="ScrollInvalidated"/> afterwards.</param>
    private void ClampOffset(Size bounds, bool notify_scroll_viewer = true)
    {
        double visible_width = bounds.Width / _zoom;
        double visible_height = bounds.Height / _zoom;
        _offset = new Vector(
            Math.Clamp(_offset.X, 0, Math.Max(0, BoardSize.Width - visible_width)),
            Math.Clamp(_offset.Y, 0, Math.Max(0, BoardSize.Height - visible_height)));
        if (notify_scroll_viewer)
        {
            RaiseScrollInvalidated(EventArgs.Empty);   // let a ScrollViewer update its scrollbars
        }
    }

    // ---- Scrolling (ILogicalScrollable): a ScrollViewer around the view shows scrollbars and sets Offset ----

    /// <summary>
    /// The size of the board at the current zoom.
    /// </summary>
    /// <remarks>
    /// In screen pixels, as a ScrollViewer expects; _offset is in board units.
    /// Within half a pixel of the view counts as fitting, so rounding never shows a scrollbar for a fitted board.
    /// </remarks>
    public Size Extent
    {
        get
        {
            Size board = BoardSize * _zoom;
            return new Size(board.Width < _viewport.Width + 0.5 ? Math.Min(board.Width, _viewport.Width) : board.Width,
                            board.Height < _viewport.Height + 0.5 ? Math.Min(board.Height, _viewport.Height) : board.Height);
        }
    }
    /// <summary>
    /// The size of the visible area, in screen pixels.
    /// </summary>
    public Size Viewport => _viewport;
    /// <summary>
    /// The arranged size, kept here because <see cref="Visual.Bounds"/> only changes after arranging.
    /// </summary>
    private Size _viewport;

    /// <summary>
    /// The scroll position in screen pixels (the board offset times the zoom). Setting it keeps it in range
    /// and redraws.
    /// </summary>
    public Vector Offset
    {
        get => _offset * _zoom;
        set
        {
            _offset = value / _zoom;
            ClampOffset(Bounds.Size, notify_scroll_viewer: false);   // the ScrollViewer set it, so it already knows
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public bool CanHorizontallyScroll { get; set; }
    /// <inheritdoc/>
    public bool CanVerticallyScroll { get; set; }
    /// <inheritdoc/>
    public bool IsLogicalScrollEnabled => true;
    /// <summary>
    /// The distance scrolled by the arrow keys and the scrollbar arrows: one cell.
    /// </summary>
    public Size ScrollSize => new Size(CellPitch * _zoom, CellPitch * _zoom);   // arrow keys / scrollbar arrows: one cell
    /// <summary>
    /// The distance scrolled by a page: the <see cref="Viewport"/>.
    /// </summary>
    public Size PageScrollSize => Viewport;

    /// <inheritdoc/>
    public event EventHandler? ScrollInvalidated;

    /// <inheritdoc/>
    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    /// <summary>
    /// Not supported: the view has no child controls to bring into view.
    /// </summary>
    /// <param name="target">The control to show.</param>
    /// <param name="targetRect">The area of the control to show.</param>
    /// <returns>Always false.</returns>
    public bool BringIntoView(Control target, Rect targetRect) => false;

    /// <summary>
    /// Not supported: the view has no child controls to navigate to.
    /// </summary>
    /// <param name="direction">The navigation direction.</param>
    /// <param name="from">The control to navigate from.</param>
    /// <returns>Always null.</returns>
    public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

    /// <summary>
    /// Converts a point in screen pixels (relative to the control) to board units.
    /// </summary>
    /// <param name="screen">The point relative to the control.</param>
    /// <returns>The point on the board.</returns>
    private Point ToBoard(Point screen) => (Point)(((Vector)screen - BoardOrigin) / _zoom + _offset);

    /// <summary>
    /// The cell under a screen point, or null outside the board or in the gap between cells.
    /// </summary>
    /// <param name="screen">The point relative to the control.</param>
    /// <returns>The cell, or null.</returns>
    private Field? CellAt(Point screen)
    {
        if (Board is null || !new Rect(Bounds.Size).Contains(screen))
        {
            return null;
        }
        Point p = ToBoard(screen);
        int column = (int)Math.Floor(p.X / CellPitch);
        int row = (int)Math.Floor(p.Y / CellPitch);
        if (row < 0 || row >= Board.Rows || column < 0 || column >= Board.Columns)
        {
            return null;
        }
        return Board[row, column];
    }

    // ---- Drawing ----

    /// <summary>
    /// Draws the visible cells at the current zoom and offset.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context)
    {
        // A control only receives the mouse where it draws something. A transparent fill makes the gaps
        // between cells and the margins around the board count too, so dragging works from anywhere.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (Board is null)
        {
            return;
        }
        TilePalette palette = _palette ??= new TilePalette(this);
        Rect area = new Rect(Bounds.Size);
        Vector origin = BoardOrigin;

        // Visible cell range.
        int first_column = Math.Max(0, (int)Math.Floor(_offset.X / CellPitch));
        int first_row = Math.Max(0, (int)Math.Floor(_offset.Y / CellPitch));
        int last_column = Math.Min(Board.Columns - 1, (int)Math.Floor((_offset.X + (area.Width - origin.X) / _zoom) / CellPitch));
        int last_row = Math.Min(Board.Rows - 1, (int)Math.Floor((_offset.Y + (area.Height - origin.Y) / _zoom) / CellPitch));

        using (context.PushClip(area))
        using (context.PushTransform(Matrix.CreateTranslation(-_offset) * Matrix.CreateScale(_zoom, _zoom)
                                     * Matrix.CreateTranslation(origin)))
        {
            for (int row = first_row; row <= last_row; row++)
            {
                for (int column = first_column; column <= last_column; column++)
                {
                    Field field = Board[row, column];
                    using (context.PushTransform(Matrix.CreateTranslation(column * CellPitch, row * CellPitch)))
                    {
                        DrawTile(context, palette, field);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Draws one cell at the origin, in board units: the tile with its hover and press look, a flag,
    /// question mark or bomb icon, and the number of a revealed cell.
    /// </summary>
    /// <param name="context">The drawing context, already transformed to the cell's position.</param>
    /// <param name="palette">The colours for the current theme.</param>
    /// <param name="field">The cell to draw.</param>
    private void DrawTile(DrawingContext context, TilePalette palette, Field field)
    {
        bool revealed = !field.Hidden;
        bool hover = ReferenceEquals(field, _hover_cell);
        bool pressed = hover && ReferenceEquals(field, _pressed_cell);

        using var shrink = TileShapes.PushPressed(context, pressed);
        IBrush background = hover ? palette.Hover : !revealed ? palette.Hidden : field.IsBomb ? palette.Bomb : palette.Clear;
        TileShapes.DrawBase(context, palette, background, raised: !revealed, hover);

        if (field.Flagged)
        {
            TileShapes.DrawIcon(context, () => TileShapes.DrawFlag(context, palette));
        }
        else if (!revealed && _questioned.Contains(field.Index))
        {
            TileShapes.DrawIcon(context, () => TileShapes.DrawQuestion(context, palette));
        }
        if (revealed && field.IsBomb)
        {
            TileShapes.DrawIcon(context, () => TileShapes.DrawBomb(context, palette));
        }

        if (revealed && field.Number is int number)
        {
            TextLayout text = palette.Number(number);
            // Placed like the old TextBlock in the 22 px area within the bevel: horizontally centred on its width
            // rounded up, at a whole pixel. The text (about 24.5 px) is taller than the area, and a TextBlock then
            // centres the overflow, so vertically it is centred exactly, without rounding.
            double inset = 1 + BevelWidth;
            double content = TileSize - 2 * inset;
            double width = Math.Ceiling(text.WidthIncludingTrailingWhitespace);
            text.Draw(context, new Point(inset + Math.Round((content - width) / 2), inset + (content - text.Height) / 2));
        }
    }

    // ---- Input ----

    /// <summary>
    /// True while a left or right button is held down on the board for a click (not while panning).
    /// The reset button shows its excited face meanwhile.
    /// </summary>
    public bool IsPressing { get; private set; }
    /// <summary>
    /// Raised when <see cref="IsPressing"/> changes.
    /// </summary>
    public event EventHandler? PressingChanged;

    /// <summary>
    /// Sets <see cref="IsPressing"/> and raises <see cref="PressingChanged"/> if it changed.
    /// </summary>
    /// <param name="pressing">The new value.</param>
    private void SetPressing(bool pressing)
    {
        if (IsPressing != pressing)
        {
            IsPressing = pressing;
            PressingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The cell under the pointer, drawn with the hover look; null while panning or locked.
    /// </summary>
    private Field? _hover_cell;
    /// <summary>
    /// The cell a left or right button was pressed on; a release on the same cell acts on it.
    /// </summary>
    private Field? _pressed_cell;
    /// <summary>
    /// The button currently held down, or <see cref="MouseButton.None"/>. Only one button is handled at a time.
    /// </summary>
    private MouseButton _pressed_button = MouseButton.None;
    /// <summary>
    /// Where the button was pressed, to tell a click from a pan.
    /// </summary>
    private Point _press_position;
    /// <summary>
    /// The previous pointer position, to pan by the distance moved since.
    /// </summary>
    private Point _last_position;
    /// <summary>
    /// True once the pointer moved more than <see cref="DragThreshold"/> with the left or middle button held down.
    /// </summary>
    private bool _panning;

    /// <summary>
    /// Starts a press: a left or right press on an unlocked board may become a click on a cell; any press
    /// may become a pan. Captures the pointer.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pressed_button != MouseButton.None)
        {
            return;   // already handling another button
        }
        PointerPoint point = e.GetCurrentPoint(this);
        _pressed_button = point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
            _ => MouseButton.None,
        };
        _press_position = _last_position = point.Position;
        bool game_action = !IsLocked && (_pressed_button == MouseButton.Left || _pressed_button == MouseButton.Right);
        _pressed_cell = game_action ? CellAt(point.Position) : null;   // locked: a press can only pan
        _panning = false;
        SetPressing(game_action);
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Turns a left or middle press into a pan once the pointer moved far enough, pans, and updates the
    /// hovered cell.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);

        bool can_pan = _pressed_button == MouseButton.Left || _pressed_button == MouseButton.Middle;
        if (can_pan && !_panning)
        {
            Vector moved = position - _press_position;
            _panning = Math.Abs(moved.X) > DragThreshold || Math.Abs(moved.Y) > DragThreshold;
            if (_panning)
            {
                _pressed_cell = null;
                SetPressing(false);
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        }
        if (_panning)
        {
            _offset -= (position - _last_position) / _zoom;
            ClampOffset(Bounds.Size);
            InvalidateVisual();
        }
        _last_position = position;

        Field? hover = _panning || IsLocked ? null : CellAt(position);
        if (!ReferenceEquals(hover, _hover_cell))
        {
            _hover_cell = hover;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Ends a press. Releasing on the pressed cell without panning reveals it (left button) or cycles its
    /// mark (right button).
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != _pressed_button)
        {
            return;
        }
        Field? cell = CellAt(e.GetPosition(this));
        if (!_panning && !IsLocked && cell is not null && ReferenceEquals(cell, _pressed_cell))
        {
            if (_pressed_button == MouseButton.Left)
            {
                Reveal(cell);
            }
            else if (_pressed_button == MouseButton.Right)
            {
                CycleMark(cell);
            }
        }
        EndPress();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    /// Cancels the current press when the pointer capture is lost.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPress();
    }

    /// <summary>
    /// Removes the hover look when the pointer leaves the view.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover_cell is not null)
        {
            _hover_cell = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Zooms around the pointer, between the fit zoom and <see cref="MaxZoom"/> (or the fit zoom, if larger).
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Board is null)
        {
            return;
        }
        Point position = e.GetPosition(this);
        double fit = FitZoom(Bounds.Size);
        double zoom = Math.Clamp(_zoom * Math.Pow(1.15, e.Delta.Y), fit, Math.Max(MaxZoom, fit));
        e.Handled = true;
        if (zoom == _zoom)
        {
            return;
        }
        // Keep the board point under the pointer in place.
        Point anchor = ToBoard(position);
        _zoom = zoom;
        _fitted = zoom <= fit;
        _offset = (Vector)anchor - ((Vector)position - BoardOrigin) / _zoom;
        ClampOffset(Bounds.Size);
        InvalidateVisual();
    }

    /// <summary>
    /// Clears the press and pan state and restores the cursor.
    /// </summary>
    private void EndPress()
    {
        SetPressing(false);
        _pressed_button = MouseButton.None;
        _pressed_cell = null;
        _panning = false;
        Cursor = null;
        InvalidateVisual();
    }

    // ---- Game actions ----

    /// <summary>
    /// Reveals a cell. The first click generates the board. On a revealed number with all its bombs flagged,
    /// it chords: the other neighbours are revealed too.
    /// </summary>
    /// <param name="field">The clicked cell.</param>
    private void Reveal(Field field)
    {
        field.Parent.Initialize(field.Position);   // the first click generates the board
        if (!field.Hidden && field.Number <= field.Neighbors.Count((Field f) => f.Flagged))
        {
            // Chord: a revealed number with all its bombs flagged reveals the other neighbours.
            foreach (Field neighbor in field.Neighbors)
            {
                if (!neighbor.Flagged)
                {
                    neighbor.Hidden = false;
                }
            }
        }
        field.Hidden = false;
    }

    /// <summary>
    /// Cycles a cell's mark: nothing -&gt; flag -&gt; question mark -&gt; nothing.
    /// </summary>
    /// <param name="field">The right-clicked cell.</param>
    private void CycleMark(Field field)
    {
        if (_questioned.Remove(field.Index))
        {
            InvalidateVisual();
            return;
        }
        if (field.Flagged)
        {
            _questioned.Add(field.Index);
        }
        field.Flagged = !field.Flagged;
        InvalidateVisual();
    }
}
