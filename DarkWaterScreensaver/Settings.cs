using System.IO;
using Microsoft.Win32;

namespace DarkWaterScreensaver;

public enum SaverMode
{
    Fixed,
    Random
}

/// <summary>Katalog der mitgelieferten Szenen unter Assets\scenes.</summary>
public static class SceneCatalog
{
    public sealed record Scene(string File, string DisplayName);

    public static readonly IReadOnlyList<Scene> All =
    [
        new("dark-water-cube-interactive.html", "Würfel"),
        new("dark-water-sphere.html", "Sphäre"),
        new("dark-water-knot.html", "Knoten"),
        new("dark-water-knot-alive.html", "Knoten (lebendig)")
    ];

    public static string ScenesRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "scenes");

    public static bool Exists(string file) =>
        All.Any(s => string.Equals(s.File, file, StringComparison.OrdinalIgnoreCase));

    /// <summary>file:///-URI der Szene inkl. ?mode=saver (blendet Hint und Cursor aus).</summary>
    public static Uri GetSaverUri(string file)
    {
        var path = Path.Combine(ScenesRoot, file);
        return new Uri(new Uri(path).AbsoluteUri + "?mode=saver");
    }

    public static string PickRandom(Random rng, string? exclude = null)
    {
        var candidates = All
            .Where(s => !string.Equals(s.File, exclude, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
            candidates = All.ToList();
        return candidates[rng.Next(candidates.Count)].File;
    }
}

/// <summary>Persistierte Einstellungen unter HKCU\Software\DarkWaterScreensaver.</summary>
public sealed class Settings
{
    private const string KeyPath = @"Software\DarkWaterScreensaver";
    public const int MinInterval = 1;
    public const int MaxInterval = 120;

    public SaverMode Mode { get; set; } = SaverMode.Fixed;
    public string SceneFile { get; set; } = SceneCatalog.All[0].File;
    public int IntervalMinutes { get; set; } = 10;

    public static Settings Load()
    {
        var settings = new Settings();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key is null)
            return settings;

        if (key.GetValue("Mode") is string mode &&
            Enum.TryParse<SaverMode>(mode, ignoreCase: true, out var parsedMode))
            settings.Mode = parsedMode;

        if (key.GetValue("SceneFile") is string scene && SceneCatalog.Exists(scene))
            settings.SceneFile = scene;

        if (key.GetValue("IntervalMinutes") is int interval)
            settings.IntervalMinutes = Math.Clamp(interval, MinInterval, MaxInterval);

        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("Mode", Mode.ToString());
        key.SetValue("SceneFile", SceneFile);
        key.SetValue("IntervalMinutes", IntervalMinutes, RegistryValueKind.DWord);
    }
}
