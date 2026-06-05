
namespace Remote.Adb.Core.Settings;

/// <summary>
/// Stores user-configurable application settings. Shared by both front-ends so a setting
/// changed in one place is observed everywhere.
/// </summary>
public interface ISettingsService
{
    /// <summary>The selected application layout density.</summary>
    AppDensity Density { get; set; }

    /// <summary>The selected application color theme.</summary>
    AppTheme Theme { get; set; }
}
