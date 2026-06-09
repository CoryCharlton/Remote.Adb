using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.Settings;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Theming;

namespace Remote.Adb.Desktop.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IAndroidSdk _androidSdk;
    private readonly IDensityApplier _densityApplier;
    private readonly ISettingsService _settings;
    private readonly IThemeApplier _themeApplier;
    private string? _avdHomeError;
    private string? _avdHomeOverride;
    private string? _javaHomeError;
    private string? _javaHomeOverride;
    private string? _sdkRootError;
    private string? _sdkRootOverride;

    public SettingsViewModel(ISettingsService settings, IAndroidSdk androidSdk, IThemeApplier themeApplier, IDensityApplier densityApplier)
    {
        _settings = settings;
        _androidSdk = androidSdk;
        _themeApplier = themeApplier;
        _densityApplier = densityApplier;

        _sdkRootOverride = settings.SdkRoot;
        _avdHomeOverride = settings.AvdHome;
        _javaHomeOverride = settings.JavaHome;
    }

    public string? AvdHomeError
    {
        get => _avdHomeError;
        private set => SetProperty(ref _avdHomeError, value);
    }

    public string? AvdHomeOverride
    {
        get => _avdHomeOverride;
        set
        {
            var ok = TryNormalizeOverride(value, out var normalized, out var error);
            SetProperty(ref _avdHomeOverride, ok ? normalized : value);
            AvdHomeError = error;
            if (ok)
            {
                _settings.AvdHome = normalized;
                OnPropertyChanged(nameof(AvdHomeStatus));
            }
        }
    }

    public string AvdHomeStatus => $"Using {AvdHome.Resolve(_settings.AvdHome) ?? "(not found)"}";

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

    public string? JavaHomeError
    {
        get => _javaHomeError;
        private set => SetProperty(ref _javaHomeError, value);
    }

    public string? JavaHomeOverride
    {
        get => _javaHomeOverride;
        set
        {
            var ok = TryNormalizeOverride(value, out var normalized, out var error);
            SetProperty(ref _javaHomeOverride, ok ? normalized : value);
            JavaHomeError = error;
            if (ok)
            {
                _settings.JavaHome = normalized;
                OnPropertyChanged(nameof(JavaStatus));
            }
        }
    }

    public string JavaStatus => $"Using {_androidSdk.JavaHome ?? "java on PATH"}";

    public string? SdkRootError
    {
        get => _sdkRootError;
        private set => SetProperty(ref _sdkRootError, value);
    }

    public string? SdkRootOverride
    {
        get => _sdkRootOverride;
        set
        {
            var ok = TryNormalizeOverride(value, out var normalized, out var error);
            SetProperty(ref _sdkRootOverride, ok ? normalized : value);
            SdkRootError = error;
            if (ok)
            {
                _settings.SdkRoot = normalized;
                OnPropertyChanged(nameof(SdkStatus));
            }
        }
    }

    public string SdkStatus => $"Using {_androidSdk.SdkRoot ?? "(not found)"} — {DescribeSource(_androidSdk.SdkRootSource)}";

    private static string DescribeSource(SdkRootSource source) => source switch
    {
        SdkRootSource.Override => "from your override",
        SdkRootSource.EnvironmentVariable => "from ANDROID_HOME",
        SdkRootSource.DefaultFallback => "default guess — set the path if this is the wrong SDK",
        _ => "not found — set the SDK path below",
    };

    // Normalizes a typed path the same way the settings store does (blank → null, otherwise trimmed), then
    // validates it: blank clears the override (valid); a non-blank value must point at an existing directory.
    // The normalized value is what gets persisted, so the validated value and the stored value always match.
    private static bool TryNormalizeOverride(string? value, out string? normalized, out string? error)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (normalized is null || Directory.Exists(normalized))
        {
            error = null;
            return true;
        }

        error = "That folder doesn't exist.";
        return false;
    }
}
