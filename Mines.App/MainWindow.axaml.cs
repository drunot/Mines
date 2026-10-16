using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Mines;

/// <summary>
/// The main window, which hosts the <see cref="Minefield"/> game.
/// </summary>
/// <remarks>
/// The window opens at the size it had last time (saved with the settings). Whenever a board of another size is
/// chosen, it fits itself to the board with cells of <see cref="BoardView.DefaultCellMillimetres"/> mm, never
/// growing past the screen; for an endless board it keeps its size. On the very first start it fits the board too.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>
    /// Creates the window, loads the saved state into the game and saves it again when the window closes.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Game.LoadState();                           // the last settings, and an unfinished game if there was one
        Closing += (sender, e) => Game.SaveState();

        bool restored = Game.WindowSize is not null;   // decided now: showing the window sets WindowSize
        if (Game.WindowSize is Size saved)
        {
            Width = saved.Width;                    // the size the player left it at
            Height = saved.Height;
        }
        Opened += (sender, e) =>
        {
            // The size of everything around the board is only known after a layout, so fit once the window is
            // open and laid out (only on the first start, without a saved size).
            Dispatcher.UIThread.Post(() =>
            {
                if (!restored)
                {
                    FitToBoard();
                }
                else
                {
                    // Saved on another (bigger) screen, perhaps: never larger than this one.
                    MinWidth = Game.MinimumWidth;
                    SetContentSize(new Size(Width, Height));
                }
            }, DispatcherPriority.Background);
        };
        SizeChanged += (sender, e) =>
        {
            if (IsVisible)
            {
                Game.WindowSize = ClientSize;       // remembered with the settings
            }
        };
        Game.BoardShown += (sender, e) =>
        {
            if (IsVisible)
            {
                // The toolbar may have changed width (two counters on an endless board); measured after layout.
                Dispatcher.UIThread.Post(() => MinWidth = Game.MinimumWidth, DispatcherPriority.Background);
            }
        };
        Game.BoardSizeChanged += (sender, e) =>
        {
            if (IsVisible)
            {
                Dispatcher.UIThread.Post(FitToBoard, DispatcherPriority.Background);
            }
        };
    }

    /// <summary>
    /// Resizes the window so the whole finite board fits at the default cell size, limited to the screen (see
    /// <see cref="SetContentSize"/>). Does nothing for an endless board.
    /// </summary>
    private void FitToBoard()
    {
        MinWidth = Game.MinimumWidth;
        if (Game.PreferredSize is Size wanted)
        {
            SetContentSize(wanted);
        }
    }

    /// <summary>
    /// Sets the size of the window's content, limited to what fits on the window's screen (the working area
    /// minus the window frame), and moves the window back onto the screen if it sticks out.
    /// </summary>
    /// <param name="wanted">The wanted content size, in device-independent units.</param>
    private void SetContentSize(Size wanted)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is not null)
        {
            // The largest window content that fits: the working area (in pixels) minus the window frame.
            double scaling = screen.Scaling;
            Size frame = FrameSize is Size outer
                ? new Size(outer.Width - ClientSize.Width, outer.Height - ClientSize.Height)
                : default;
            double maxWidth = screen.WorkingArea.Width / scaling - frame.Width;
            double maxHeight = screen.WorkingArea.Height / scaling - frame.Height;
            wanted = new Size(Math.Min(wanted.Width, maxWidth), Math.Min(wanted.Height, maxHeight));
        }
        Width = Math.Ceiling(wanted.Width);
        Height = Math.Ceiling(wanted.Height);

        if (screen is not null)
        {
            // Keep the whole window on the screen.
            PixelRect area = screen.WorkingArea;
            Size outer = FrameSize ?? new Size(Width, Height);
            int width = (int)Math.Ceiling(outer.Width * screen.Scaling);
            int height = (int)Math.Ceiling(outer.Height * screen.Scaling);
            int x = Math.Max(area.X, Math.Min(Position.X, area.Right - width));
            int y = Math.Max(area.Y, Math.Min(Position.Y, area.Bottom - height));
            if (x != Position.X || y != Position.Y)
            {
                Position = new PixelPoint(x, y);
            }
        }
    }
}
