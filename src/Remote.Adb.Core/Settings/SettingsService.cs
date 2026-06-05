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
}
