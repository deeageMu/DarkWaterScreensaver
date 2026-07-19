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
    private readonly Settings _settings = Settings.Load();
    private readonly Random _rng = new();
    private readonly List<ScreensaverWindow> _windows = [];
    private string _currentScene = "";

    public SaverController(Application app) => _app = app;

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

        foreach (var monitor in monitors)
        {
            var window = new ScreensaverWindow(monitor);
            _windows.Add(window);
            window.Show();
            window.NavigateToScene(_currentScene);
        }

        InputWatcher.Start(OnUserInput);
        _app.Exit += (_, _) => InputWatcher.Stop();

        if (_settings.Mode == SaverMode.Random && SceneCatalog.All.Count > 1)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(_settings.IntervalMinutes)
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
