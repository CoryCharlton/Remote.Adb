namespace Remote.Adb.Core.Settings;

/// <summary>
/// <see cref="ISettingsService"/> backed by an <see cref="ISettingsStore"/>: the persisted model is loaded
/// once on construction and re-saved whenever a setting changes.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly SettingsModel _model;
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
        _model = _store.Load();
    }

    public string? AvdHome
    {
        get => _model.AvdHome;
        set => SetOverride(_model.AvdHome, value, normalized => _model.AvdHome = normalized);
    }

    public AppDensity Density
    {
        get => _model.Density;
        set
        {
            if (_model.Density == value)
            {
                return;
            }

            _model.Density = value;
            _store.Save(_model);
        }
    }

    public string? JavaHome
    {
        get => _model.JavaHome;
        set => SetOverride(_model.JavaHome, value, normalized => _model.JavaHome = normalized);
    }

    public string? SdkRoot
    {
        get => _model.SdkRoot;
        set => SetOverride(_model.SdkRoot, value, normalized => _model.SdkRoot = normalized);
    }

    public AppTheme Theme
    {
        get => _model.Theme;
        set
        {
            if (_model.Theme == value)
            {
                return;
            }

            _model.Theme = value;
            _store.Save(_model);
        }
    }

    public bool TunnelAutoConnect
    {
        get => _model.TunnelAutoConnect;
        set
        {
            if (_model.TunnelAutoConnect == value)
            {
                return;
            }

            _model.TunnelAutoConnect = value;
            _store.Save(_model);
        }
    }

    public string? TunnelHost
    {
        get => _model.TunnelHost;
        set => SetOverride(_model.TunnelHost, value, normalized => _model.TunnelHost = normalized);
    }

    public int TunnelLocalPort
    {
        get => _model.TunnelLocalPort;
        set
        {
            if (_model.TunnelLocalPort == value)
            {
                return;
            }

            _model.TunnelLocalPort = value;
            _store.Save(_model);
        }
    }

    public int TunnelRemotePort
    {
        get => _model.TunnelRemotePort;
        set
        {
            if (_model.TunnelRemotePort == value)
            {
                return;
            }

            _model.TunnelRemotePort = value;
            _store.Save(_model);
        }
    }

    // A blank or whitespace path means "no override" — store it as null so it round-trips as absent.
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SetOverride(string? current, string? value, Action<string?> assign)
    {
        var normalized = Normalize(value);
        if (current == normalized)
        {
            return;
        }

        assign(normalized);
        _store.Save(_model);
    }
}
