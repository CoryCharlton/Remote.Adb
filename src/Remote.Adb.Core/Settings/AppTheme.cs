namespace Remote.Adb.Core.Settings;

/// <summary>
/// The application color theme. UI-agnostic so it can be persisted in Core and mapped
/// to the host toolkit's theme by each front-end.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow the operating system's light/dark preference.</summary>
    System,

    /// <summary>The light theme.</summary>
    Light,

    /// <summary>The dark theme.</summary>
    Dark,
}
