using System;
using Avalonia;
using Avalonia.Controls;
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
/// <para>
/// Mouse wheel zooms around the pointer; dragging (left or middle button) pans.
/// Left click reveals (and chords on a revealed number), right click cycles flag -&gt; question mark -&gt; nothing;
/// the board (<see cref="IBoard"/>) applies the rules.
/// </para>
/// <para>
/// Cells start at <see cref="DefaultCellMillimetres"/> mm and can be zoomed in up to <see cref="MaxZoomFactor"/>
/// times that. A finite board can be zoomed out until it fits and shows scrollbars inside a ScrollViewer
/// (<see cref="ILogicalScrollable"/>); an infinite board can be zoomed out to <see cref="InfiniteMinZoomFactor"/>
/// times the default, has no scrollbars and pans freely, and the view tells it which cells are visible
/// (<see cref="IBoard.SetVisibleArea"/>).
/// </para>
/// </remarks>
public class BoardView : Control, ILogicalScrollable
{
    /// <summary>
    /// Defines the <see cref="Board"/> property.
    /// </summary>
    public static readonly StyledProperty<IBoard?> BoardProperty =
        AvaloniaProperty.Register<BoardView, IBoard?>(nameof(Board));

    /// <summary>
    /// The board shown: finite or infinite. Setting a new board resets the zoom to <see cref="DefaultZoom"/>
    /// (cells of <see cref="DefaultCellMillimetres"/> mm) and shows the top-left corner of a finite board, or
    /// cell (0, 0) in the middle of an infinite one. Use <see cref="ShowCell"/> afterwards to show another part.
    /// </summary>
    public IBoard? Board
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
    private const double DragThreshold = 4;  // pixels the pointer may move before a press becomes a pan

    /// <summary>
    /// The size of a cell (tile plus gap) at the start, in millimetres.
    /// </summary>
    public const double DefaultCellMillimetres = 5;

    /// <summary>
    /// The zoom at which a cell is <see cref="DefaultCellMillimetres"/> mm. Avalonia measures in device-independent
    /// units of 1/96 inch (the operating system's scaling maps them to the screen), so this is
    /// 5 mm = 5 / 25.4 * 96 units, divided by the cell pitch.
    /// </summary>
    public static double DefaultZoom => DefaultCellMillimetres / 25.4 * 96 / CellPitch;

    /// <summary>
    /// Zooming in stops at cells this many times <see cref="DefaultCellMillimetres"/>.
    /// </summary>
    private const double MaxZoomFactor = 4;

    /// <summary>
    /// Zooming out on an infinite board stops at cells this share of <see cref="DefaultCellMillimetres"/>. It also
    /// limits how many cells are visible, and so how far a cascade can open at once.
    /// </summary>
    private const double InfiniteMinZoomFactor = 0.5;

    /// <summary>
    /// Screen pixels per board unit.
    /// </summary>
    private double _zoom = DefaultZoom;
    private Vector _offset;                  // board point shown at the top-left of the visible area
    private Vector? _centerTarget;          // board point to put in the middle once the view's size is known

    // ---- Board ----

    /// <summary>
    /// Reacts to <see cref="IsLocked"/> (drops the hover and press state when locking) and to
    /// <see cref="Board"/> (follows the new board's changes and resets the view).
    /// </summary>
    /// <param name="change">The property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLockedProperty && IsLocked)
        {
            _hoverCell = null;
            _pressedCell = null;
            SetPressing(false);
            InvalidateVisual();
        }
        if (change.Property == BoardProperty)
        {
            if (change.OldValue is IBoard oldBoard)
            {
                oldBoard.FieldChanged -= OnBoardChanged;
            }
            if (change.NewValue is IBoard newBoard)
            {
                newBoard.FieldChanged += OnBoardChanged;
            }
            _zoom = DefaultZoom;
            _offset = default;
            _hoverCell = null;
            _pressedCell = null;
            _centerTarget = IsInfinite ? new Vector(TileSize / 2, TileSize / 2) : null;   // cell (0, 0)
            if (_centerTarget is not null && _viewport.Width > 0)
            {
                ApplyCenterTarget();
            }
            ClampOffset(_viewport);
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

    // ---- Size and coordinates ----

    /// <summary>
    /// Whether the board shown is infinite.
    /// </summary>
    private bool IsInfinite => Board is not null && Board.Size is null;

    /// <summary>
    /// The size of the whole board in board units (no gap after the last row and column), or empty without a
    /// board or for an infinite board.
    /// </summary>
    private Size BoardSize => Board?.Size is BoardSize size
        ? new Size(size.Columns * CellPitch - (CellPitch - TileSize), size.Rows * CellPitch - (CellPitch - TileSize))
        : default;

    /// <summary>
    /// The size the view needs to show the whole of a finite board at <see cref="DefaultZoom"/>, in
    /// device-independent units; <c>null</c> for an infinite board or without a board. The window uses it to fit
    /// itself to a new board.
    /// </summary>
    public Size? PreferredSize => Board?.Size is null ? null : BoardSize * DefaultZoom;

    /// <summary>
    /// The smallest zoom: a finite board can be zoomed out until it fits (but not below the default if it already
    /// fits); an infinite board down to <see cref="InfiniteMinZoomFactor"/> times the default.
    /// </summary>
    /// <param name="bounds">The size of the visible area, in screen pixels.</param>
    private double MinZoom(Size bounds) => IsInfinite
        ? DefaultZoom * InfiniteMinZoomFactor
        : Math.Min(FitZoom(bounds), DefaultZoom);

    /// <summary>
    /// The largest zoom: <see cref="MaxZoomFactor"/> times the default.
    /// </summary>
    private static double MaxZoom => DefaultZoom * MaxZoomFactor;

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
    private Vector BoardOrigin => IsInfinite ? default : new Vector(
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
    /// Records the viewport size, keeps the zoom between the limits, applies a waiting centre target once the
    /// size is known (see <see cref="ApplyCenterTarget"/>), and keeps the offset in range.
    /// </summary>
    /// <param name="finalSize">The size given to the view.</param>
    /// <returns>The size used.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size size = base.ArrangeOverride(finalSize);
        _viewport = size;   // Bounds only changes after arranging, but the ScrollViewer asks during it
        _zoom = Math.Clamp(_zoom, MinZoom(size), Math.Max(MaxZoom, MinZoom(size)));
        if (_centerTarget is not null && size.Width > 0)
        {
            ApplyCenterTarget();
        }
        ClampOffset(size);
        return size;
    }

    /// <summary>
    /// Puts the waiting centre target (<see cref="ShowCell"/>, or cell (0, 0) of a new infinite board) in the
    /// middle of the view.
    /// </summary>
    private void ApplyCenterTarget()
    {
        if (_centerTarget is not Vector target)
        {
            return;
        }
        _centerTarget = null;
        _offset = target - new Vector(_viewport.Width, _viewport.Height) / _zoom / 2;
    }

    /// <summary>
    /// The cell in the middle of the view, in cells (fractions allowed): X is the row, Y the column. With
    /// <see cref="ZoomFactor"/>, enough to show the same part of the board again with <see cref="ShowCell"/>.
    /// </summary>
    public Vector CenterCell => _centerTarget is Vector target
        ? new Vector(target.Y, target.X) / CellPitch
        : new Vector(_offset.Y + _viewport.Height / _zoom / 2, _offset.X + _viewport.Width / _zoom / 2) / CellPitch;

    /// <summary>
    /// The zoom as a multiple of the default (cells of <see cref="DefaultCellMillimetres"/> mm).
    /// </summary>
    public double ZoomFactor => _zoom / DefaultZoom;

    /// <summary>
    /// Shows a part of the board: puts a cell in the middle at a zoom (kept within the zoom limits). Used to
    /// restore the view of a saved game; call it after setting <see cref="Board"/>.
    /// </summary>
    /// <param name="row">The row to put in the middle (fractions allowed).</param>
    /// <param name="column">The column to put in the middle (fractions allowed).</param>
    /// <param name="zoomFactor">The zoom as a multiple of the default.</param>
    public void ShowCell(double row, double column, double zoomFactor)
    {
        _zoom = DefaultZoom * zoomFactor;
        _centerTarget = new Vector(column, row) * CellPitch;
        if (_viewport.Width > 0)
        {
            _zoom = Math.Clamp(_zoom, MinZoom(_viewport), Math.Max(MaxZoom, MinZoom(_viewport)));
            ApplyCenterTarget();
            ClampOffset(_viewport);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Keeps a finite board from being dragged out of view (an infinite board has no limits), and tells the board
    /// which cells are visible now.
    /// </summary>
    /// <param name="bounds">The size of the visible area, in screen pixels.</param>
    /// <param name="notifyScrollViewer">Whether to raise <see cref="ScrollInvalidated"/> afterwards.</param>
    private void ClampOffset(Size bounds, bool notifyScrollViewer = true)
    {
        if (!IsInfinite)
        {
            double visibleWidth = bounds.Width / _zoom;
            double visibleHeight = bounds.Height / _zoom;
            _offset = new Vector(
                Math.Clamp(_offset.X, 0, Math.Max(0, BoardSize.Width - visibleWidth)),
                Math.Clamp(_offset.Y, 0, Math.Max(0, BoardSize.Height - visibleHeight)));
        }
        if (notifyScrollViewer)
        {
            RaiseScrollInvalidated(EventArgs.Empty);   // let a ScrollViewer update its scrollbars
        }
        UpdateVisibleArea(bounds);
    }

    /// <summary>
    /// Tells the board which cells are visible (<see cref="IBoard.SetVisibleArea"/>), so an infinite board
    /// can continue a cascade that reached the cells now in view.
    /// </summary>
    /// <param name="bounds">The size of the visible area, in screen pixels.</param>
    private void UpdateVisibleArea(Size bounds)
    {
        if (Board is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        Vector origin = BoardOrigin;
        Board.SetVisibleArea(
            (long)Math.Floor(_offset.Y / CellPitch),
            (long)Math.Floor(_offset.X / CellPitch),
            (long)Math.Floor((_offset.Y + (bounds.Height - origin.Y) / _zoom) / CellPitch),
            (long)Math.Floor((_offset.X + (bounds.Width - origin.X) / _zoom) / CellPitch));
    }

    // ---- Scrolling (ILogicalScrollable): a ScrollViewer around the view shows scrollbars and sets Offset ----

    /// <summary>
    /// The size of the board at the current zoom; for an infinite board just the viewport, so no scrollbars show.
    /// </summary>
    /// <remarks>
    /// In screen pixels, as a ScrollViewer expects; <c>_offset</c> is in board units.
    /// Within half a pixel of the view counts as fitting, so rounding never shows a scrollbar for a fitted board.
    /// </remarks>
    public Size Extent
    {
        get
        {
            if (IsInfinite)
            {
                return _viewport;
            }
            Size board = BoardSize * _zoom;
            return new Size(
                board.Width < _viewport.Width + 0.5 ? Math.Min(board.Width, _viewport.Width) : board.Width,
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
    /// and redraws. An infinite board has no scroll position: it is always 0 and setting it does nothing.
    /// </summary>
    public Vector Offset
    {
        get => IsInfinite ? default : _offset * _zoom;
        set
        {
            if (IsInfinite)
            {
                return;
            }
            _offset = value / _zoom;
            ClampOffset(Bounds.Size, notifyScrollViewer: false);   // the ScrollViewer set it, so it already knows
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
    // arrow keys / scrollbar arrows: one cell
    public Size ScrollSize => new Size(CellPitch * _zoom, CellPitch * _zoom);
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
    /// The cell under a screen point (X is the row, Y the column).
    /// </summary>
    /// <param name="screen">The point relative to the control.</param>
    /// <returns>
    /// The cell's global position, or null without a board, outside the control or outside a finite board.
    /// </returns>
    private Point<long>? CellAt(Point screen)
    {
        if (Board is null || !new Rect(Bounds.Size).Contains(screen))
        {
            return null;
        }
        Point p = ToBoard(screen);
        long column = (long)Math.Floor(p.X / CellPitch);
        long row = (long)Math.Floor(p.Y / CellPitch);
        if (Board.Size is BoardSize size && (row < 0 || row >= size.Rows || column < 0 || column >= size.Columns))
        {
            return null;
        }
        return new Point<long>(row, column);
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

        // Visible cell range; a finite board also stops at its edges.
        long firstColumn = (long)Math.Floor(_offset.X / CellPitch);
        long firstRow = (long)Math.Floor(_offset.Y / CellPitch);
        long lastColumn = (long)Math.Floor((_offset.X + (area.Width - origin.X) / _zoom) / CellPitch);
        long lastRow = (long)Math.Floor((_offset.Y + (area.Height - origin.Y) / _zoom) / CellPitch);
        if (Board.Size is BoardSize size)
        {
            firstColumn = Math.Max(0, firstColumn);
            firstRow = Math.Max(0, firstRow);
            lastColumn = Math.Min(size.Columns - 1, lastColumn);
            lastRow = Math.Min(size.Rows - 1, lastRow);
        }

        // Positions relative to the first visible cell keep the numbers small, however far an infinite board
        // has been panned.
        Vector first = new Vector(firstColumn * CellPitch, firstRow * CellPitch);
        using (context.PushClip(area))
        using (context.PushTransform(Matrix.CreateTranslation(first - _offset) * Matrix.CreateScale(_zoom, _zoom)
                                     * Matrix.CreateTranslation(origin)))
        {
            for (long row = firstRow; row <= lastRow; row++)
            {
                for (long column = firstColumn; column <= lastColumn; column++)
                {
                    var cell = new Point<long>(row, column);
                    using (context.PushTransform(Matrix.CreateTranslation((column - firstColumn) * CellPitch,
                                                                          (row - firstRow) * CellPitch)))
                    {
                        DrawTile(context, palette, Board.FieldAt(row, column), cell);
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
    /// <param name="field">The cell to draw, or <c>null</c> for a part of an infinite board that doesn't exist yet
    /// (drawn as a hidden cell).</param>
    /// <param name="cell">The cell's position, for the hover and press look.</param>
    private void DrawTile(DrawingContext context, TilePalette palette, Field? field, Point<long> cell)
    {
        bool revealed = field is not null && !field.Hidden;
        bool hover = Nullable.Equals(_hoverCell, cell);
        bool pressed = hover && Nullable.Equals(_pressedCell, cell);

        using var shrink = TileShapes.PushPressed(context, pressed);
        IBrush background = hover ? palette.Hover
            : !revealed ? palette.Hidden
            : field!.IsBomb ? palette.Bomb
            : palette.Clear;
        TileShapes.DrawBase(context, palette, background, raised: !revealed, hover);
        if (field is null)
        {
            return;
        }

        if (field.Flagged)
        {
            TileShapes.DrawIcon(context, () => TileShapes.DrawFlag(context, palette));
        }
        else if (field.Questioned)
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
            text.Draw(context,
                new Point(inset + Math.Round((content - width) / 2), inset + (content - text.Height) / 2));
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
    /// The cell under the pointer, drawn with the hover look; null while panning or locked, or when the pointer is
    /// off the board.
    /// </summary>
    private Point<long>? _hoverCell;
    /// <summary>
    /// The cell a left or right button was pressed on; a release on the same cell acts on it.
    /// </summary>
    private Point<long>? _pressedCell;
    /// <summary>
    /// The button currently held down, or <see cref="MouseButton.None"/>. Only one button is handled at a time.
    /// </summary>
    private MouseButton _pressedButton = MouseButton.None;
    /// <summary>
    /// Where the button was pressed, to tell a click from a pan.
    /// </summary>
    private Point _pressPosition;
    /// <summary>
    /// The previous pointer position, to pan by the distance moved since.
    /// </summary>
    private Point _lastPosition;
    /// <summary>
    /// True once the pointer moved more than <see cref="DragThreshold"/> with the left or middle button held down.
    /// </summary>
    private bool _panning;

    /// <summary>
    /// Starts a press: a left or right press on an unlocked board may become a click on a cell; a left or middle
    /// press may become a pan. Captures the pointer.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pressedButton != MouseButton.None)
        {
            return;   // already handling another button
        }
        PointerPoint point = e.GetCurrentPoint(this);
        _pressedButton = point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButton.Left,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
            _ => MouseButton.None,
        };
        _pressPosition = _lastPosition = point.Position;
        bool gameAction = !IsLocked && (_pressedButton == MouseButton.Left || _pressedButton == MouseButton.Right);
        _pressedCell = gameAction ? CellAt(point.Position) : null;   // locked: a press can only pan
        _panning = false;
        SetPressing(gameAction);
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

        bool canPan = _pressedButton == MouseButton.Left || _pressedButton == MouseButton.Middle;
        if (canPan && !_panning)
        {
            Vector moved = position - _pressPosition;
            _panning = Math.Abs(moved.X) > DragThreshold || Math.Abs(moved.Y) > DragThreshold;
            if (_panning)
            {
                _pressedCell = null;
                SetPressing(false);
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        }
        if (_panning)
        {
            _offset -= (position - _lastPosition) / _zoom;
            ClampOffset(Bounds.Size);
            InvalidateVisual();
        }
        _lastPosition = position;

        Point<long>? hover = _panning || IsLocked ? null : CellAt(position);
        if (!Nullable.Equals(hover, _hoverCell))
        {
            _hoverCell = hover;
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
        if (e.InitialPressMouseButton != _pressedButton)
        {
            return;
        }
        Point<long>? cell = CellAt(e.GetPosition(this));
        if (!_panning && !IsLocked && cell is Point<long> c && Nullable.Equals(cell, _pressedCell))
        {
            if (_pressedButton == MouseButton.Left)
            {
                Board?.Reveal(c.X, c.Y);
            }
            else if (_pressedButton == MouseButton.Right)
            {
                Board?.CycleMark(c.X, c.Y);
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
        if (_hoverCell is not null)
        {
            _hoverCell = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Zooms around the pointer, between <see cref="MinZoom"/> and <see cref="MaxZoom"/>.
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
        double min = MinZoom(Bounds.Size);
        double zoom = Math.Clamp(_zoom * Math.Pow(1.15, e.Delta.Y), min, Math.Max(MaxZoom, min));
        e.Handled = true;
        if (zoom == _zoom)
        {
            return;
        }
        // Keep the board point under the pointer in place.
        Point anchor = ToBoard(position);
        _zoom = zoom;
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
        _pressedButton = MouseButton.None;
        _pressedCell = null;
        _panning = false;
        Cursor = null;
        InvalidateVisual();
    }
}
