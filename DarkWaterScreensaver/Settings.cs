using System.IO;
using Microsoft.Win32;

namespace DarkWaterScreensaver;

public enum SaverMode
{
    Fixed,
    Random
}

/// <summary>Catalogue of the bundled scenes under Assets\scenes.</summary>
public static class SceneCatalog
{
    /// <param name="SupportsEffects">
    /// True for scenes that carry the in-page effect checkboxes (bolts /
    /// colouring). Those scenes read ?bolts=0|1 and ?colorall=0|1; the
    /// settings dialog is the source of truth for them.
    /// </param>
    public sealed record Scene(string File, string DisplayName, bool SupportsEffects = false);

    public static readonly IReadOnlyList<Scene> All =
    [
        new("dark-water-cube-interactive.html", "Cube"),
        new("dark-water-sphere.html", "Sphere"),
        new("dark-water-knot.html", "Knot"),
        new("dark-water-knot-alive.html", "Knot (alive)"),
        new("dark-water-octahedron.html", "Octahedron", SupportsEffects: true),
        new("dark-water-truncated-octahedron-hover.html",
            "Truncated octahedron (hover)", SupportsEffects: true),
        new("dark-water-dive-fast.html", "Dive (fast)", SupportsEffects: true)
    ];

    public static string ScenesRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "scenes");

    public static Scene? Find(string file) =>
        All.FirstOrDefault(s => string.Equals(s.File, file, StringComparison.OrdinalIgnoreCase));

    public static bool Exists(string file) => Find(file) is not null;

    /// <summary>True if any scene offers the bolts / colouring checkboxes.</summary>
    public static bool AnySupportsEffects => All.Any(s => s.SupportsEffects);

    /// <summary>
    /// file:/// URI of the scene. Saver mode appends ?mode=saver (hides hint,
    /// in-page panel and cursor). glow, bolts and colorall are always written
    /// explicitly, because the scenes differ in what they default to when a
    /// parameter is missing.
    /// </summary>
    public static Uri GetUri(string file, bool saverMode, Settings settings)
    {
        var path = Path.Combine(ScenesRoot, file);
        var uri = new Uri(path).AbsoluteUri;
        var query = new List<string>();
        if (saverMode) query.Add("mode=saver");
        query.Add(settings.Glow ? "glow=1" : "glow=0");
        if (Find(file)?.SupportsEffects == true)
        {
            query.Add(settings.Bolts ? "bolts=1" : "bolts=0");
            query.Add(settings.ColorAll ? "colorall=1" : "colorall=0");
        }
        return new Uri(uri + "?" + string.Join("&", query));
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

/// <summary>Settings persisted under HKCU\Software\DarkWaterScreensaver.</summary>
public sealed class Settings
{
    private const string KeyPath = @"Software\DarkWaterScreensaver";
    public const int MinInterval = 5;
    public const int MaxInterval = 3600;

    public SaverMode Mode { get; set; } = SaverMode.Fixed;
    public string SceneFile { get; set; } = SceneCatalog.All[0].File;
    public int IntervalSeconds { get; set; } = 30;
    public bool Glow { get; set; }

    /// <summary>Lightning fronts sweeping over the body (scene checkbox "Lightning").</summary>
    public bool Bolts { get; set; } = true;

    /// <summary>
    /// Colouring (scene checkbox "Colouring"): every inner-glow light gets its own
    /// random hue instead of only every fifth one.
    /// </summary>
    public bool ColorAll { get; set; }

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

        if (key.GetValue("Bolts") is int bolts)
            settings.Bolts = bolts != 0;

        if (key.GetValue("ColorAll") is int colorAll)
            settings.ColorAll = colorAll != 0;

        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("Mode", Mode.ToString());
        key.SetValue("SceneFile", SceneFile);
        key.SetValue("IntervalSeconds", IntervalSeconds, RegistryValueKind.DWord);
        key.SetValue("Glow", Glow ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("Bolts", Bolts ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("ColorAll", ColorAll ? 1 : 0, RegistryValueKind.DWord);
    }
}
