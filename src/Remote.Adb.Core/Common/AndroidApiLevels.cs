using System.Collections.Frozen;

namespace Remote.Adb.Core.Common;

/// <summary>
/// Maps an Android API level to its marketing version and codename (e.g. 36 → "Android 16 (Baklava)"). Used to
/// label system images and AVDs more recognizably than the bare level.
/// </summary>
public static class AndroidApiLevels
{
    // Dessert/food codename per API level, where one is well-known. Android 10–12L (29–32) dropped public
    // codenames, so they're omitted rather than guessed.
    private static readonly FrozenDictionary<int, string> Codenames = new Dictionary<int, string>
    {
        [21] = "Lollipop",
        [22] = "Lollipop",
        [23] = "Marshmallow",
        [24] = "Nougat",
        [25] = "Nougat",
        [26] = "Oreo",
        [27] = "Oreo",
        [28] = "Pie",
        [33] = "Tiramisu",
        [34] = "Upside Down Cake",
        [35] = "Vanilla Ice Cream",
        [36] = "Baklava",
    }.ToFrozenDictionary();

    // Marketing version per API level. Post-10 releases dropped public dessert names, so just the number.
    private static readonly FrozenDictionary<int, string> Versions = new Dictionary<int, string>
    {
        [21] = "5.0",
        [22] = "5.1",
        [23] = "6.0",
        [24] = "7.0",
        [25] = "7.1",
        [26] = "8.0",
        [27] = "8.1",
        [28] = "9",
        [29] = "10",
        [30] = "11",
        [31] = "12",
        [32] = "12L",
        [33] = "13",
        [34] = "14",
        [35] = "15",
        [36] = "16",
    }.ToFrozenDictionary();

    /// <summary>The Android codename for an API level (e.g. 36 → "Baklava"), or <see langword="null"/> if none.</summary>
    public static string? Codename(int apiLevel) => Codenames.GetValueOrDefault(apiLevel);

    /// <summary>e.g. 34 → "Android 14"; an unknown level → "API 34".</summary>
    public static string DisplayName(int apiLevel) =>
        Versions.TryGetValue(apiLevel, out var version) ? $"Android {version}" : $"API {apiLevel}";

    /// <summary>The version with its codename — e.g. 36 → "Android 16 (Baklava)"; no codename → "Android 16".</summary>
    public static string Label(int apiLevel) =>
        Codename(apiLevel) is { } codename ? $"{DisplayName(apiLevel)} ({codename})" : DisplayName(apiLevel);

    /// <summary>
    /// Parses the numeric API level from an <c>android-&lt;n&gt;</c> token, tolerating a minor-version suffix —
    /// e.g. "android-34", "34", and "android-36.1" yield 34, 34, and 36 (the major level). Returns
    /// <see langword="false"/> when there's no leading integer (e.g. a preview codename like "android-Baklava").
    /// </summary>
    public static bool TryParseLevel(string token, out int apiLevel)
    {
        const string prefix = "android-";
        var value = token.StartsWith(prefix, StringComparison.Ordinal) ? token[prefix.Length..] : token;
        var major = value.Split('.', 2)[0];

        return int.TryParse(major, out apiLevel);
    }

    /// <summary>The marketing version string (e.g. 34 → "14"), or <see langword="null"/> if unknown.</summary>
    public static string? Version(int apiLevel) =>
        Versions.TryGetValue(apiLevel, out var version) ? version : null;
}
