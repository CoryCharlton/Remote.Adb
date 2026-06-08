using CommunityToolkit.Mvvm.ComponentModel;

namespace Remote.Adb.Desktop.Common;

/// <summary>
/// A titled group of <see cref="AvdField"/>s in a details pane, rendered as a divider-headed section whose
/// rows flip read-only ↔ editable with <see cref="IsEditing"/>. The whole section hides when it has no
/// visible rows (empty groups in read mode, fully read-only groups while editing).
/// </summary>
public partial class DetailGroup : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool _isEditing;

    public DetailGroup(string header, IReadOnlyList<AvdField> rows)
    {
        Header = header;
        Rows = rows;
    }

    public string Header { get; }

    public bool IsVisible => Rows.Any(row => row.RowVisible);

    public IReadOnlyList<AvdField> Rows { get; }
}
