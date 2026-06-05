using System;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Shared conventions for the AVD config values that aren't free text — the yes/no booleans and the
/// memory/storage sizes. Pure and I/O-free so the editor's validation and any normalization agree on one
/// definition. The editor keeps the raw config string as the value; these are validation predicates, not
/// converters.
/// </summary>
public static partial class AvdValueConventions
{
    /// <summary>How <c>config.ini</c> encodes booleans (e.g. <c>hw.gps</c>, <c>hw.gpu.enabled</c>).</summary>
    public static readonly FrozenSet<string> BooleanValues =
        new[] { "no", "yes" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="value"/> is a valid AVD id (the <c>avdmanager create avd -n</c> argument and
    /// the <c>.avd</c> folder name): letters, digits, <c>.</c>, <c>_</c>, or <c>-</c>.
    /// </summary>
    public static bool IsValidAvdName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && AvdNameRegex().IsMatch(value.Trim());

    /// <summary>
    /// Whether <paramref name="value"/> is a positive whole number (used for counts such as CPU cores and
    /// LCD density).
    /// </summary>
    public static bool IsValidCount(string? value) =>
        int.TryParse((value ?? string.Empty).Trim(), out var count) && count > 0;

    /// <summary>
    /// Whether <paramref name="value"/> is a valid memory/storage size: a positive whole number, optionally
    /// suffixed with a <c>K</c>/<c>M</c>/<c>G</c> unit (and an optional trailing <c>B</c>) — e.g. <c>2048</c>,
    /// <c>512M</c>, <c>2G</c>, <c>4GB</c>.
    /// </summary>
    public static bool IsValidSize(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SizeRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex AvdNameRegex();

    [GeneratedRegex(@"^\d+\s*([KMG]B?)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SizeRegex();
}
