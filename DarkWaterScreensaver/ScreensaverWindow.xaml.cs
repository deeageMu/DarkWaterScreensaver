using System.Windows;
using System.Windows.Interop;

namespace DarkWaterScreensaver;

public partial class ScreensaverWindow : Window
{
    private readonly Win32.RECT _monitorRect;
    private string? _sceneFile;
    private bool _webViewReady;

    internal ScreensaverWindow(Win32.RECT monitorRect)
    {
        InitializeComponent();
        _monitorRect = monitorRect;
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Pixelgenau auf den Zielmonitor legen (PerMonitorV2-DPI-aware),
        // deckt dort auch die Taskleiste ab.
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST,
            _monitorRect.Left, _monitorRect.Top, _monitorRect.Width, _monitorRect.Height,
            Win32.SWP_SHOWWINDOW);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebViewHost.InitializeAsync(WebView);
        }
        catch
        {
            // Ohne WebView2-Runtime kann nichts gerendert werden — Screensaver beenden.
            Application.Current.Shutdown();
            return;
        }
        _webViewReady = true;
        TryNavigate();
    }

    public void NavigateToScene(string sceneFile)
    {
        _sceneFile = sceneFile;
        TryNavigate();
    }

    private void TryNavigate()
    {
        if (_webViewReady && _sceneFile is not null)
            WebView.CoreWebView2.Navigate(SceneCatalog.GetSaverUri(_sceneFile).AbsoluteUri);
    }
}
