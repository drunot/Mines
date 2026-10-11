using Avalonia.Controls;

namespace Mines;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Game.LoadState();                           // the last settings, and an unfinished game if there was one
        Closing += (sender, e) => Game.SaveState();
    }
}
