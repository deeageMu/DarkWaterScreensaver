using System.Windows;
using System.Windows.Controls;

namespace DarkWaterScreensaver;

public partial class SettingsWindow : Window
{
    private readonly List<RadioButton> _sceneButtons = [];

    public SettingsWindow()
    {
        InitializeComponent();

        var settings = Settings.Load();

        foreach (var scene in SceneCatalog.All)
        {
            var button = new RadioButton
            {
                Content = scene.DisplayName,
                Tag = scene.File,
                GroupName = "Scene",
                Margin = new Thickness(0, 2, 0, 2),
                IsChecked = string.Equals(scene.File, settings.SceneFile, StringComparison.OrdinalIgnoreCase)
            };
            button.Checked += OnSceneSelected;
            _sceneButtons.Add(button);
            ScenePanel.Children.Add(button);
        }
        if (!_sceneButtons.Any(b => b.IsChecked == true))
            _sceneButtons[0].IsChecked = true;

        RandomCheckBox.IsChecked = settings.Mode == SaverMode.Random;
        IntervalTextBox.Text = settings.IntervalSeconds.ToString();
        GlowCheckBox.IsChecked = settings.Glow;
        BoltsCheckBox.IsChecked = settings.Bolts;
        ColorCheckBox.IsChecked = settings.ColorAll;
        UpdateEnabledState();
    }

    private void OnRandomToggled(object sender, RoutedEventArgs e) => UpdateEnabledState();

    private void OnSceneSelected(object sender, RoutedEventArgs e) => UpdateEnabledState();

    private void UpdateEnabledState()
    {
        var random = RandomCheckBox.IsChecked == true;
        foreach (var button in _sceneButtons)
            button.IsEnabled = !random;
        IntervalTextBox.IsEnabled = random;

        // Bolts / colouring only exist in some scenes; in random mode every scene
        // may come up, so the effect options stay available there.
        var effects = random
            ? SceneCatalog.AnySupportsEffects
            : SceneCatalog.Find(SelectedScene)?.SupportsEffects == true;
        BoltsCheckBox.IsEnabled = effects;
        ColorCheckBox.IsEnabled = effects;
        EffectsHint.Visibility = effects ? Visibility.Collapsed : Visibility.Visible;
    }

    private string SelectedScene =>
        _sceneButtons.FirstOrDefault(b => b.IsChecked == true)?.Tag as string
        ?? SceneCatalog.All[0].File;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalTextBox.Text.Trim(), out var interval))
            interval = 10;
        interval = Math.Clamp(interval, Settings.MinInterval, Settings.MaxInterval);

        var settings = new Settings
        {
            Mode = RandomCheckBox.IsChecked == true ? SaverMode.Random : SaverMode.Fixed,
            SceneFile = SelectedScene,
            IntervalSeconds = interval,
            Glow = GlowCheckBox.IsChecked == true,
            Bolts = BoltsCheckBox.IsChecked == true,
            ColorAll = ColorCheckBox.IsChecked == true
        };
        settings.Save();
        Close();
    }
}
