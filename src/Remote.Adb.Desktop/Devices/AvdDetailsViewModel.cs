using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Devices;

/// <summary>
/// The details pane for one AVD: its full configuration grouped into collapsible sections that flip between
/// read-only display and editable inputs. Created by <see cref="DevicesViewModel"/>'s ViewDetails;
/// <see cref="BackCommand"/> returns to the list. Edit collects only the touched fields and persists them via
/// <see cref="IAvdConfigStore"/>, clearing (omitting) any field left blank.
/// </summary>
public partial class AvdDetailsViewModel : ViewModelBase
{
    private readonly Action _back;
    private readonly IAvdConfigStore _store;
    private string _avdId = string.Empty;
    private IReadOnlyList<AvdField> _fields = [];

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<DetailGroup> _groups = [];

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    public AvdDetailsViewModel(AvdConfiguration configuration, IAvdConfigStore store, Action back)
    {
        _store = store;
        _back = back;
        Load(configuration);
    }

    [RelayCommand]
    private void Back() => _back();

    private static string BuildSubtitle(AvdConfiguration config) =>
        config.ApiLevel is { } api ? $"API {api}  ·  {config.AvdId}" : config.AvdId;

    [RelayCommand]
    private void Cancel()
    {
        foreach (var field in _fields)
        {
            field.Reset();
        }

        StatusMessage = null;
        SetEditing(false);
    }

    [RelayCommand]
    private void Edit()
    {
        StatusMessage = null;
        SetEditing(true);
    }

    private void Load(AvdConfiguration config)
    {
        _avdId = config.AvdId;
        DisplayName = config.DisplayName;
        Subtitle = BuildSubtitle(config);

        var built = AvdDetailFields.BuildAll(config);
        Groups = built.Groups;
        _fields = built.Fields;
        IsEditing = false;
    }

    [RelayCommand]
    private void Save()
    {
        var hasErrors = false;
        foreach (var field in _fields.Where(field => !field.IsReadOnly))
        {
            if (!field.Validate())
            {
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            StatusMessage = "Fix the highlighted fields before saving.";
            return;
        }

        var dirty = _fields.Where(field => !field.IsReadOnly && field.IsDirty).ToList();
        if (dirty.Count == 0)
        {
            SetEditing(false);
            return;
        }

        var changes = dirty
            .Where(field => field.HasValue)
            .ToDictionary(field => field.Key, field => (field.Value ?? string.Empty).Trim(), StringComparer.Ordinal);
        var removals = dirty
            .Where(field => !field.HasValue)
            .Select(field => field.Key)
            .ToList();

        var updated = _store.Write(_avdId, changes, removals);
        if (updated is null)
        {
            StatusMessage = "Could not save changes.";
            return;
        }

        Load(updated);
    }

    private void SetEditing(bool editing)
    {
        IsEditing = editing;

        foreach (var field in _fields)
        {
            field.IsEditing = editing;
        }

        foreach (var group in Groups)
        {
            group.IsEditing = editing;
        }
    }
}
