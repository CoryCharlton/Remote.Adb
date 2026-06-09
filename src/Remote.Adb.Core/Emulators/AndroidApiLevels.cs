using System.Collections.Frozen;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Maps an Android API level to its marketing version (e.g. 34 → "Android 14"). Used to label system images
/// and AVD details more recognizably than the bare level.
/// </summary>
public static class AndroidApiLevels
{
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

    /// <summary>e.g. 34 → "Android 14"; an unknown level → "API 34".</summary>
    public static string DisplayName(int apiLevel) =>
        Versions.TryGetValue(apiLevel, out var version) ? $"Android {version}" : $"API {apiLevel}";

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
