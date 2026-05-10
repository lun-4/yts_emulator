using Avalonia;

namespace YtsEmulator;

public static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        System.Console.SetOut(new System.IO.StreamWriter(System.Console.OpenStandardOutput()) { AutoFlush = true });
        System.Console.WriteLine("[YTS] starting Avalonia…");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
