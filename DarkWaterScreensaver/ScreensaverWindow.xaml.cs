using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DarkWaterScreensaver;

public partial class ScreensaverWindow : Window
{
    private readonly Win32.RECT _monitorRect;
    private readonly bool _interactive;
    private readonly bool _glow;
    private string? _sceneFile;
    private bool _webViewReady;

    internal ScreensaverWindow(Win32.RECT monitorRect, bool interactive = false, bool glow = false)
    {
        InitializeComponent();
        _monitorRect = monitorRect;
        _interactive = interactive;
        _glow = glow;

        if (interactive)
        {
            // /i-Modus: Maus/Zoom steuern die Szene, nur Escape beendet.
            Cursor = Cursors.Arrow;
            Topmost = false;
            ShowInTaskbar = true;
            PreviewKeyDown += OnEscapeKey;
            WebView.KeyDown += OnEscapeKey;
        }

        Loaded += OnLoaded;
    }

    private void OnEscapeKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
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
            WebView.CoreWebView2.Navigate(SceneCatalog.GetUri(_sceneFile, saverMode: !_interactive, glow: _glow).AbsoluteUri);
    }
}
