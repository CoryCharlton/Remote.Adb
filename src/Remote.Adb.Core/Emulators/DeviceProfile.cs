using System.Globalization;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// A device definition usable as the <c>-d</c> argument to <c>avdmanager create avd</c>. The id/name/OEM/tag
/// come from <c>avdmanager list device</c>; the screen specs (when present) come from the richer device
/// definition XML and drive the create wizard's device table and form-factor grouping.
/// </summary>
/// <param name="Id">The device id (e.g. <c>pixel_6</c>) — passed to <c>avdmanager create avd -d</c>.</param>
/// <param name="Name">The friendly device name (e.g. "Pixel 6").</param>
/// <param name="Oem">The OEM/manufacturer, or <see langword="null"/> if not reported.</param>
/// <param name="Tag">The device category tag (e.g. <c>android-tv</c>, <c>android-wear</c>), or
/// <see langword="null"/> for a general phone/tablet.</param>
/// <param name="ScreenWidth">Screen width in pixels, or <see langword="null"/> if unknown.</param>
/// <param name="ScreenHeight">Screen height in pixels, or <see langword="null"/> if unknown.</param>
/// <param name="Density">Screen density in dpi, or <see langword="null"/> if unknown.</param>
/// <param name="RamMb">Default RAM in MB, or <see langword="null"/> if unknown.</param>
/// <param name="ScreenSize">The screen-size bucket (<c>normal</c>/<c>large</c>/<c>xlarge</c>), used to tell
/// phones from tablets.</param>
/// <param name="MinApi">The minimum supported API level (e.g. 24), or <see langword="null"/> if unknown.</param>
/// <param name="PlayStore">Whether the device supports Google Play Store images.</param>
/// <param name="IsObsolete">Whether the device is a deprecated/obsolete profile (hidden by default).</param>
/// <param name="DiagonalInches">The screen's diagonal length in inches, or <see langword="null"/>.</param>
public sealed record DeviceProfile(
    string Id,
    string Name,
    string? Oem,
    string? Tag = null,
    int? ScreenWidth = null,
    int? ScreenHeight = null,
    int? Density = null,
    int? RamMb = null,
    string? ScreenSize = null,
    int? MinApi = null,
    bool PlayStore = false,
    bool IsObsolete = false,
    double? DiagonalInches = null)
{
    /// <summary>The diagonal length as a label, e.g. "6.4\"" (or empty when unknown).</summary>
    public string DiagonalLabel => DiagonalInches is { } inches
        ? $"{inches.ToString("0.0", CultureInfo.InvariantCulture)}\""
        : string.Empty;

    /// <summary>The minimum supported API as a label, e.g. "24+" (or empty when unknown).</summary>
    public string SupportedApi => MinApi is { } api ? $"{api}+" : string.Empty;

    /// <summary>The screen resolution as "W × H" (or empty when unknown).</summary>
    public string Resolution => ScreenWidth is { } width && ScreenHeight is { } height ? $"{width} × {height}" : string.Empty;

    /// <summary>The form factor: <c>phone</c>, <c>tablet</c>, <c>wear</c>, <c>tv</c>, <c>automotive</c>,
    /// <c>desktop</c>, or <c>xr</c>. Phones and tablets share the "general" tag, so they're split by screen size.</summary>
    public string FormFactor
    {
        get
        {
            var category = AvdCategories.Of(Tag);
            if (category != "general")
            {
                return category;
            }

            if (ScreenSize is "large" or "xlarge")
            {
                return "tablet";
            }

            // The avdmanager fallback carries no screen size; infer tablet from the name/id rather than
            // calling everything a phone, which would drop "Tablet" from the wizard's form-factor list.
            if (ScreenSize is null && LooksLikeTablet)
            {
                return "tablet";
            }

            return "phone";
        }
    }

    /// <summary>Heuristic tablet detection for profiles with no screen-size metadata (the avdmanager fallback).</summary>
    private bool LooksLikeTablet =>
        Id.Contains("tablet", StringComparison.OrdinalIgnoreCase)
        || Name.Contains("tablet", StringComparison.OrdinalIgnoreCase);
}
