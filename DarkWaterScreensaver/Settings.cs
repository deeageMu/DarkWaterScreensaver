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

    /// <summary>
    /// file:/// URI of the scene. Saver mode appends ?mode=saver (hides hint
    /// and cursor); glow=1 enables the inner glow effect.
    /// </summary>
    public static Uri GetUri(string file, bool saverMode, bool glow = false)
    {
        var path = Path.Combine(ScenesRoot, file);
        var uri = new Uri(path).AbsoluteUri;
        var query = new List<string>();
        if (saverMode) query.Add("mode=saver");
        if (glow) query.Add("glow=1");
        return new Uri(query.Count > 0 ? uri + "?" + string.Join("&", query) : uri);
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
    public const int MinInterval = 5;
    public const int MaxInterval = 3600;

    public SaverMode Mode { get; set; } = SaverMode.Fixed;
    public string SceneFile { get; set; } = SceneCatalog.All[0].File;
    public int IntervalSeconds { get; set; } = 30;
    public bool Glow { get; set; }

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

        if (key.GetValue("IntervalSeconds") is int interval)
            settings.IntervalSeconds = Math.Clamp(interval, MinInterval, MaxInterval);

        if (key.GetValue("Glow") is int glow)
            settings.Glow = glow != 0;

        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("Mode", Mode.ToString());
        key.SetValue("SceneFile", SceneFile);
        key.SetValue("IntervalSeconds", IntervalSeconds, RegistryValueKind.DWord);
        key.SetValue("Glow", Glow ? 1 : 0, RegistryValueKind.DWord);
    }
}
