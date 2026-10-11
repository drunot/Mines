using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Mines;

/// <summary>
/// A button drawn like a board cell: a raised tile with the cells' hover and press look.
/// </summary>
/// <remarks>
/// Subclasses draw the icon on it (<see cref="DrawIcon"/>). Scales with its size (32 x 32 like a cell by
/// default).
/// </remarks>
public abstract class TileButton : Control
{
    /// <summary>
    /// Raised when the button is pressed and released with the left mouse button while over it.
    /// </summary>
    public event EventHandler? Click;

    /// <summary>
    /// Initializes a new instance of the <see cref="TileButton"/> class, which looks up its colours again and
    /// redraws when the theme changes.
    /// </summary>
    protected TileButton()
    {
        ActualThemeVariantChanged += (sender, e) =>
        {
            _palette = null;   // colours come from the theme; look them up again
            InvalidateVisual();
        };
    }

    /// <summary>
    /// Draws the icon in tile units (a 32 x 32 tile), e.g. with <see cref="TileShapes.DrawIcon"/>.
    /// </summary>
    /// <param name="context">The drawing context, already transformed to tile units.</param>
    /// <param name="palette">The theme brushes to draw with.</param>
    private protected abstract void DrawIcon(DrawingContext context, TilePalette palette);

    /// <summary>
    /// Asks for the size of one tile (32 x 32).
    /// </summary>
    /// <param name="availableSize">The available size (ignored).</param>
    /// <returns>The desired size.</returns>
    protected override Size MeasureOverride(Size availableSize) => new Size(TileShapes.TileSize, TileShapes.TileSize);

    /// <summary>
    /// Draws the tile, scaled to the largest square that fits and centred, with its hover and pressed look,
    /// and then the icon.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public override void Render(DrawingContext context)
    {
        TilePalette palette = _palette ??= new TilePalette(this);
        double scale = Math.Min(Bounds.Width, Bounds.Height) / TileShapes.TileSize;
        double size = TileShapes.TileSize * scale;
        bool pressed = _pressed && _hover;

        using (context.PushTransform(Matrix.CreateScale(scale, scale)
                                     * Matrix.CreateTranslation((Bounds.Width - size) / 2, (Bounds.Height - size) / 2)))
        using (TileShapes.PushPressed(context, pressed))
        {
            TileShapes.DrawBase(context, palette, _hover ? palette.Hover : palette.Hidden, raised: true, _hover);
            DrawIcon(context, palette);
        }
    }

    // ---- Input ----

    /// <summary>
    /// Whether the pointer is over the button.
    /// </summary>
    private bool _hover;

    /// <summary>
    /// Whether the left mouse button was pressed on the button and not yet released (the pointer is captured).
    /// </summary>
    private bool _pressed;

    /// <inheritdoc/>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    /// <summary>
    /// Starts a press on the left mouse button and captures the pointer.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _pressed = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// While pressed, tracks whether the pointer is still over the button.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressed)
        {
            // While captured, Entered/Exited don't fire; track whether the pointer is still over the button.
            bool over = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            if (over != _hover)
            {
                _hover = over;
                InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Ends a left-button press, releases the pointer and raises <see cref="Click"/> if the pointer is still
    /// over the button.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        bool clicked = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        InvalidateVisual();
        if (clicked)
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Cancels the press (without a <see cref="Click"/>) when the pointer capture is lost.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        InvalidateVisual();
    }

    /// <summary>
    /// The theme brushes, created on first render and dropped when the theme changes.
    /// </summary>
    private TilePalette? _palette;
}
