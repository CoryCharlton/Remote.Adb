using CommunityToolkit.Mvvm.ComponentModel;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Desktop.Devices;

/// <summary>
/// A single device row: a mutable, bindable projection of <see cref="AdbDevice"/>. Rows are updated in place
/// across refreshes (see <see cref="Update"/>), keyed by <see cref="Serial"/>, so list state survives a reload.
/// </summary>
public partial class DeviceRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private string? _summary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    private string _state = string.Empty;

    public DeviceRowViewModel(AdbDevice device)
    {
        Serial = device.Serial;
        Update(device);
    }

    /// <summary>The adb serial — the immutable identity used to match rows across refreshes.</summary>
    public string Serial { get; }

    /// <summary>A friendly connection state for display (online devices read "Online").</summary>
    public string StateLabel => IsOnline ? "Online" : State;

    /// <summary>Applies the latest snapshot to this row, preserving its identity.</summary>
    public void Update(AdbDevice device)
    {
        DisplayName = string.IsNullOrEmpty(device.Model) ? device.Serial : device.Model;
        IsOnline = device.IsOnline;
        State = device.State;
        Summary = string.Join(" · ", new[] { device.Product, device.Device }.Where(part => !string.IsNullOrEmpty(part)));
    }
}
