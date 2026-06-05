using Avalonia;
using Avalonia.Styling;
using Remote.Adb.Core.Settings;

namespace Remote.Adb.Desktop.Theming;

/// <inheritdoc cref="IThemeApplier"/>
public sealed class ThemeApplier : IThemeApplier
{
    public void Apply(AppTheme theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToVariant(theme);
        }
    }

    private static ThemeVariant ToVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
