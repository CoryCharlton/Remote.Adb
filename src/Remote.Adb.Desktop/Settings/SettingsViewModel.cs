using Remote.Adb.Core.Settings;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Theming;

namespace Remote.Adb.Desktop.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IDensityApplier _densityApplier;
    private readonly ISettingsService _settings;
    private readonly IThemeApplier _themeApplier;

    public SettingsViewModel(ISettingsService settings, IThemeApplier themeApplier, IDensityApplier densityApplier)
    {
        _settings = settings;
        _themeApplier = themeApplier;
        _densityApplier = densityApplier;
    }

    // Reads through to the settings service rather than caching, so a recreated view always reflects the live
    // density — the toggle can never desync from what is actually applied.
    public bool IsCompactDensity
    {
        get => _settings.Density == AppDensity.Compact;
        set
        {
            var density = value ? AppDensity.Compact : AppDensity.Normal;
            if (_settings.Density == density)
            {
                return;
            }

            _settings.Density = density;
            _densityApplier.Apply(density);
            OnPropertyChanged();
        }
    }

    // Reads through to the settings service rather than caching, so a recreated view always reflects the live
    // theme — the toggle can never desync from what is actually applied.
    public bool IsLightTheme
    {
        get => _settings.Theme == AppTheme.Light;
        set
        {
            var theme = value ? AppTheme.Light : AppTheme.Dark;
            if (_settings.Theme == theme)
            {
                return;
            }

            _settings.Theme = theme;
            _themeApplier.Apply(theme);
            OnPropertyChanged();
        }
    }
}
