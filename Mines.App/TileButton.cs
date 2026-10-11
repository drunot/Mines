using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Mines;

// A button drawn like a board cell: a raised tile with the cells' hover and press look. Subclasses draw
// the icon on it (DrawIcon). Scales with its size (32 x 32 like a cell by default).
public abstract class TileButton : Control
{
    // Raised when the button is pressed and released with the left mouse button while over it.
    public event EventHandler? Click;

    protected TileButton()
    {
        ActualThemeVariantChanged += (sender, e) =>
        {
            _palette = null;   // colours come from the theme; look them up again
            InvalidateVisual();
        };
    }

    // Draws the icon in tile units (a 32 x 32 tile), e.g. with TileShapes.DrawIcon.
    private protected abstract void DrawIcon(DrawingContext context, TilePalette palette);

    protected override Size MeasureOverride(Size availableSize) => new Size(TileShapes.TileSize, TileShapes.TileSize);

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

    private bool _hover;
    private bool _pressed;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

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

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        InvalidateVisual();
    }

    private TilePalette? _palette;
}
