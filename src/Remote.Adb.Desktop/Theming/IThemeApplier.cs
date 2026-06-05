using Remote.Adb.Core.Settings;

namespace Remote.Adb.Desktop.Theming;

/// <summary>
/// Applies an <see cref="AppTheme"/> to the running Avalonia application. Confines the
/// <c>Application.Current</c> static to one place so view models stay testable.
/// </summary>
public interface IThemeApplier
{
    /// <summary>Applies <paramref name="theme"/> to the application's requested theme variant.</summary>
    void Apply(AppTheme theme);
}
