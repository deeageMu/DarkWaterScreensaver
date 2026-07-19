using System.Windows;
using System.Windows.Interop;

namespace DarkWaterScreensaver;

internal enum LaunchMode
{
    Saver,
    Configure,
    Preview,
    Interactive
}

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var (mode, hwnd) = ParseCommandLine(args);

        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };

        switch (mode)
        {
            case LaunchMode.Preview:
                if (hwnd == IntPtr.Zero)
                    return 0;
                return app.Run(new PreviewWindow(hwnd));

            case LaunchMode.Configure:
                var settingsWindow = new SettingsWindow();
                if (hwnd != IntPtr.Zero)
                    new WindowInteropHelper(settingsWindow) { Owner = hwnd };
                return app.Run(settingsWindow);

            case LaunchMode.Interactive:
                var interactiveController = new SaverController(app, interactive: true);
                interactiveController.Start();
                return app.Run();

            case LaunchMode.Saver:
            default:
                var controller = new SaverController(app);
                controller.Start();
                return app.Run();
        }
    }

    /// <summary>
    /// Windows ruft .scr-Dateien je nach Kontext unterschiedlich auf:
    /// ohne Argument, /s, -s, /c, /c:HWND, /c HWND, /p HWND, /p:HWND —
    /// Groß-/Kleinschreibung und Trennzeichen variieren.
    /// </summary>
    private static (LaunchMode Mode, IntPtr Hwnd) ParseCommandLine(string[] args)
    {
        if (args.Length == 0)
            return (LaunchMode.Configure, IntPtr.Zero);

        var first = args[0].Trim();
        var switchChar = first.TrimStart('/', '-');

        // "/c:12345" bzw. "/p:12345"
        string? inlineArg = null;
        var colon = switchChar.IndexOf(':');
        if (colon >= 0)
        {
            inlineArg = switchChar[(colon + 1)..];
            switchChar = switchChar[..colon];
        }

        var hwndText = inlineArg ?? (args.Length > 1 ? args[1] : null);
        var hwnd = ParseHwnd(hwndText);

        return switchChar.ToLowerInvariant() switch
        {
            "s" => (LaunchMode.Saver, IntPtr.Zero),
            "p" => (LaunchMode.Preview, hwnd),
            "i" or "interactive" => (LaunchMode.Interactive, IntPtr.Zero),
            _ => (LaunchMode.Configure, hwnd)
        };
    }

    private static IntPtr ParseHwnd(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && long.TryParse(text.Trim(), out var value))
            return new IntPtr(value);
        return IntPtr.Zero;
    }
}
