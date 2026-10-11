using Avalonia;
using System;

namespace Mines;

/// <summary>
/// The application's entry point.
/// </summary>
class Program
{
    /// <summary>
    /// Initialization code. Starts the app with a classic desktop lifetime.
    /// </summary>
    /// <remarks>
    /// Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant code before
    /// AppMain is called: things aren't initialized yet and stuff might break.
    /// </remarks>
    /// <param name="args">The command line arguments.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Avalonia configuration, don't remove; also used by visual designer.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/> for <see cref="App"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
