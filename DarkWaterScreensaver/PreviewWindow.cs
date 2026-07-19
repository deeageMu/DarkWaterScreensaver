using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;

namespace DarkWaterScreensaver;

/// <summary>
/// /p-Modus: rendert die konfigurierte Szene als Miniaturvorschau in das von
/// Windows übergebene HWND (kleines Monitorbild im Anzeigeeinstellungen-Dialog).
/// Das eigene Fenster wird per SetParent als WS_CHILD eingebettet und auf das
/// Client-Rect des Parents skaliert.
/// </summary>
public sealed class PreviewWindow : Window
{
    private readonly IntPtr _parentHwnd;
    private readonly WebView2 _webView = new();

    public PreviewWindow(IntPtr parentHwnd)
    {
        _parentHwnd = parentHwnd;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = System.Windows.Media.Brushes.Black;
        Content = _webView;

        Loaded += OnLoaded;

        // Windows beendet den Preview-Prozess nicht zwingend selbst — Parent überwachen.
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        watchdog.Tick += (_, _) =>
        {
            if (!Win32.IsWindow(_parentHwnd))
                Application.Current.Shutdown();
        };
        watchdog.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, (style | Win32.WS_CHILD) & ~Win32.WS_POPUP);
        Win32.SetParent(hwnd, _parentHwnd);

        Win32.GetClientRect(_parentHwnd, out var rect);
        Win32.MoveWindow(hwnd, 0, 0, rect.Width, rect.Height, true);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebViewHost.InitializeAsync(_webView);
        }
        catch
        {
            return; // Vorschau bleibt schwarz, wenn keine WebView2-Runtime vorhanden ist.
        }

        var settings = Settings.Load();
        var scene = settings.Mode == SaverMode.Random
            ? SceneCatalog.PickRandom(new Random())
            : settings.SceneFile;
        _webView.CoreWebView2.Navigate(SceneCatalog.GetSaverUri(scene).AbsoluteUri);
    }
}
