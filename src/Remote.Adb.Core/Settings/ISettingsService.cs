
namespace Remote.Adb.Core.Settings;

/// <summary>
/// Stores user-configurable application settings. Shared by both front-ends so a setting
/// changed in one place is observed everywhere.
/// </summary>
public interface ISettingsService
{
    /// <summary>Override for the AVD home directory, or <see langword="null"/> to use env-var/default resolution.</summary>
    string? AvdHome { get; set; }

    /// <summary>The selected application layout density.</summary>
    AppDensity Density { get; set; }

    /// <summary>Override for the Java home (JDK) directory, or <see langword="null"/> to use <c>JAVA_HOME</c>/PATH.</summary>
    string? JavaHome { get; set; }

    /// <summary>Override for the Android SDK root, or <see langword="null"/> to use env-var/default resolution.</summary>
    string? SdkRoot { get; set; }

    /// <summary>The selected application color theme.</summary>
    AppTheme Theme { get; set; }
}
