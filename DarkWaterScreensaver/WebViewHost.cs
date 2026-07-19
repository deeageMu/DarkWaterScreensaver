using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DarkWaterScreensaver;

internal static class WebViewHost
{
    private static Task<CoreWebView2Environment>? _environmentTask;

    /// <summary>
    /// Gemeinsame WebView2-Umgebung mit User-Data-Folder unter %LOCALAPPDATA% —
    /// zwingend, weil der Screensaver aus einem schreibgeschützten Verzeichnis
    /// (z. B. System32) laufen kann.
    /// </summary>
    public static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        return _environmentTask ??= CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DarkWaterScreensaver", "WebView2"));
    }

    public static async Task InitializeAsync(WebView2 webView)
    {
        webView.DefaultBackgroundColor = System.Drawing.Color.Black;
        var environment = await GetEnvironmentAsync();
        await webView.EnsureCoreWebView2Async(environment);

        var settings = webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
    }
}
