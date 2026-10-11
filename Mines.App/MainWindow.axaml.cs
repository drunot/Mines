using Avalonia.Controls;

namespace Mines;

/// <summary>
/// The main window, which hosts the <see cref="Minefield"/> game.
/// </summary>
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
    }
}
