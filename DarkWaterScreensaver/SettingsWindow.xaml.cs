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
            _sceneButtons.Add(button);
            ScenePanel.Children.Add(button);
        }
        if (!_sceneButtons.Any(b => b.IsChecked == true))
            _sceneButtons[0].IsChecked = true;

        RandomCheckBox.IsChecked = settings.Mode == SaverMode.Random;
        IntervalTextBox.Text = settings.IntervalSeconds.ToString();
        GlowCheckBox.IsChecked = settings.Glow;
        UpdateEnabledState();
    }

    private void OnRandomToggled(object sender, RoutedEventArgs e) => UpdateEnabledState();

    private void UpdateEnabledState()
    {
        var random = RandomCheckBox.IsChecked == true;
        foreach (var button in _sceneButtons)
            button.IsEnabled = !random;
        IntervalTextBox.IsEnabled = random;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalTextBox.Text.Trim(), out var interval))
            interval = 10;
        interval = Math.Clamp(interval, Settings.MinInterval, Settings.MaxInterval);

        var settings = new Settings
        {
            Mode = RandomCheckBox.IsChecked == true ? SaverMode.Random : SaverMode.Fixed,
            SceneFile = _sceneButtons.FirstOrDefault(b => b.IsChecked == true)?.Tag as string
                        ?? SceneCatalog.All[0].File,
            IntervalSeconds = interval,
            Glow = GlowCheckBox.IsChecked == true
        };
        settings.Save();
        Close();
    }
}
