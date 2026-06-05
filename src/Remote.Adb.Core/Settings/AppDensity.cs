namespace Remote.Adb.Core.Settings;

/// <summary>
/// The application layout density. UI-agnostic so it can be persisted in Core and mapped to the host
/// toolkit's density by each front-end (the desktop maps it to CCSWE.Avalonia.Material's <c>DensityStyle</c>).
/// </summary>
public enum AppDensity
{
    /// <summary>The default Material 3 sizing (full-size touch targets).</summary>
    Normal,

    /// <summary>A denser, desktop-oriented sizing that shrinks heights and paddings.</summary>
    Compact,
}
