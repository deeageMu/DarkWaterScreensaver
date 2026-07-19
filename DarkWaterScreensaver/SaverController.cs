using System.Windows;
using System.Windows.Threading;

namespace DarkWaterScreensaver;

/// <summary>
/// /s-Modus: öffnet ein Vollbildfenster pro Monitor (gleiche Szene gespiegelt),
/// installiert die globalen Input-Hooks und wechselt bei Mode=Random die Szene
/// per Timer. Der programmatische Szenenwechsel (Navigate) erzeugt keinen
/// Hardware-Input und löst den Exit daher nicht aus.
/// </summary>
internal sealed class SaverController
{
    private readonly Application _app;
    private readonly bool _interactive;
    private readonly Settings _settings = Settings.Load();
    private readonly Random _rng = new();
    private readonly List<ScreensaverWindow> _windows = [];
    private string _currentScene = "";

    public SaverController(Application app, bool interactive = false)
    {
        _app = app;
        _interactive = interactive;
    }

    public void Start()
    {
        _currentScene = _settings.Mode == SaverMode.Random
            ? SceneCatalog.PickRandom(_rng)
            : _settings.SceneFile;

        var monitors = Win32.GetMonitorRects();
        if (monitors.Count == 0)
        {
            monitors.Add(new Win32.RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)SystemParameters.PrimaryScreenWidth,
                Bottom = (int)SystemParameters.PrimaryScreenHeight
            });
        }

        if (_interactive)
        {
            // Interaktiv nur ein Fenster auf dem Primärmonitor (Ursprung 0,0).
            var primary = monitors.FirstOrDefault(m => m.Left == 0 && m.Top == 0);
            monitors = [primary.Width > 0 ? primary : monitors[0]];
        }

        foreach (var monitor in monitors)
        {
            var window = new ScreensaverWindow(monitor, _interactive, _settings.Glow);
            _windows.Add(window);
            window.Show();
            window.NavigateToScene(_currentScene);
        }

        if (!_interactive)
        {
            InputWatcher.Start(OnUserInput);
            _app.Exit += (_, _) => InputWatcher.Stop();
        }

        if (_settings.Mode == SaverMode.Random && SceneCatalog.All.Count > 1)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.IntervalSeconds)
            };
            timer.Tick += (_, _) => SwitchToNextRandomScene();
            timer.Start();
        }
    }

    private void SwitchToNextRandomScene()
    {
        _currentScene = SceneCatalog.PickRandom(_rng, exclude: _currentScene);
        foreach (var window in _windows)
            window.NavigateToScene(_currentScene);
    }

    private void OnUserInput()
    {
        _app.Dispatcher.BeginInvoke(() => _app.Shutdown());
    }
}
