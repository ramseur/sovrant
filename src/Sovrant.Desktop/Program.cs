using Avalonia;

namespace Sovrant.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Phase 144: .env first, before App's statics (SOVRANT_USER_ID, …) read the environment.
        Sovrant.Runtime.Config.BootstrapConfigLoader.EnsureDotEnvLoaded();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
