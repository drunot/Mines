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

// The whole board in one control. Only the visible cells are drawn, so the cost depends on the window
// size, not the board size. Mouse wheel zooms around the pointer; dragging (left or middle button) pans.
// Left click reveals (and chords on a revealed number), right click cycles flag -> question mark -> nothing.
// Inside a ScrollViewer it shows scrollbars (ILogicalScrollable), while still drawing only the visible cells.
public class BoardView : Control, ILogicalScrollable
{
    public static readonly StyledProperty<FieldMatrix?> BoardProperty =
        AvaloniaProperty.Register<BoardView, FieldMatrix?>(nameof(Board));

    public FieldMatrix? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<BoardView, bool>(nameof(IsLocked));

    // A finished game: clicks no longer reveal or mark cells and cells show no hover or press look.
    // Zooming and panning still work, to look at the result.
    public bool IsLocked
    {
        get => GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    static BoardView()
    {
        ClipToBoundsProperty.OverrideDefaultValue<BoardView>(true);
    }

    public BoardView()
    {
        ActualThemeVariantChanged += (sender, e) =>
        {
            _palette = null;   // colours come from the theme; look them up again
            InvalidateVisual();
        };
    }

    // ---- Layout, in board units (zoom 1). Matches the old 32 px button tiles with a 1 px gap. ----

    private const double CellPitch = 33;
    private const double TileSize = TileShapes.TileSize;
    private const double BevelWidth = TileShapes.BevelWidth;
    private const double MaxZoom = 4;        // zooming in stops here (or at the fit, if that is larger)
    private const double DragThreshold = 4;  // pixels the pointer may move before a press becomes a pan

    private double _zoom = 1;
    private bool _fitted = true;             // at the fit zoom; stays fitted when the size changes
    private Vector _offset;                  // board point shown at the top-left of the visible area

    // ---- Board ----

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

    private void OnBoardChanged(object? sender, EventArgs e) => InvalidateVisual();

    private TilePalette? _palette;   // colours for the current theme

    // Question marks are only a reminder for the player, so they live here, not in the game logic.
    private readonly HashSet<int> _questioned = new();

    // The question-marked cells (Field.Index), e.g. to save them; SetQuestionMarks restores them.
    public IReadOnlyCollection<int> QuestionMarks => _questioned;

    public void SetQuestionMarks(IEnumerable<int> cells)
    {
        _questioned.Clear();
        _questioned.UnionWith(cells);
        InvalidateVisual();
    }

    // ---- Size and coordinates ----

    private Size BoardSize => Board is null
        ? default
        : new Size(Board.Columns * CellPitch - (CellPitch - TileSize), Board.Rows * CellPitch - (CellPitch - TileSize));

    // The zoom at which the whole board just fits; zooming out stops here.
    private double FitZoom(Size bounds)
    {
        if (BoardSize.Width <= 0 || BoardSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return 1;
        }
        return Math.Min(bounds.Width / BoardSize.Width, bounds.Height / BoardSize.Height);
    }

    // Where the board's top-left corner is drawn: centred along a direction in which it is smaller than the view.
    private Vector BoardOrigin => new Vector(
        Math.Max(0, (Bounds.Width - BoardSize.Width * _zoom) / 2),
        Math.Max(0, (Bounds.Height - BoardSize.Height * _zoom) / 2));

    // The view fills the space it is given (the ScrollViewer's visible area).
    protected override Size MeasureOverride(Size availableSize)
    {
        Size full = BoardSize * _zoom;
        return new Size(double.IsInfinity(availableSize.Width) ? full.Width : availableSize.Width,
                        double.IsInfinity(availableSize.Height) ? full.Height : availableSize.Height);
    }

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

    // Keeps the board from being dragged out of view.
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

    // In screen pixels, as a ScrollViewer expects; _offset is in board units.
    // Within half a pixel of the view counts as fitting, so rounding never shows a scrollbar for a fitted board.
    public Size Extent
    {
        get
        {
            Size board = BoardSize * _zoom;
            return new Size(board.Width < _viewport.Width + 0.5 ? Math.Min(board.Width, _viewport.Width) : board.Width,
                            board.Height < _viewport.Height + 0.5 ? Math.Min(board.Height, _viewport.Height) : board.Height);
        }
    }
    public Size Viewport => _viewport;
    private Size _viewport;

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

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public bool IsLogicalScrollEnabled => true;
    public Size ScrollSize => new Size(CellPitch * _zoom, CellPitch * _zoom);   // arrow keys / scrollbar arrows: one cell
    public Size PageScrollSize => Viewport;

    public event EventHandler? ScrollInvalidated;

    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    public bool BringIntoView(Control target, Rect targetRect) => false;

    public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

    private Point ToBoard(Point screen) => (Point)(((Vector)screen - BoardOrigin) / _zoom + _offset);

    // The cell under a screen point, or null outside the board or in the gap between cells.
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

    // True while a left or right button is held down on the board for a click (not while panning).
    // The reset button shows its excited face meanwhile.
    public bool IsPressing { get; private set; }
    public event EventHandler? PressingChanged;

    private void SetPressing(bool pressing)
    {
        if (IsPressing != pressing)
        {
            IsPressing = pressing;
            PressingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Field? _hover_cell;
    private Field? _pressed_cell;
    private MouseButton _pressed_button = MouseButton.None;
    private Point _press_position;
    private Point _last_position;
    private bool _panning;

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

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPress();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover_cell is not null)
        {
            _hover_cell = null;
            InvalidateVisual();
        }
    }

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

    // Nothing -> flag -> question mark -> nothing.
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
