using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// A single emulator row: a mutable, bindable projection of <see cref="AndroidVirtualDevice"/>
/// plus the transient <see cref="IsStarting"/> state the list shows while an AVD is booting.
/// Rows are updated in place across refreshes (see <see cref="Update"/>) so selection and the
/// starting state survive a reload.
/// </summary>
public partial class EmulatorDeviceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStart))]
    [NotifyPropertyChangedFor(nameof(ShowStop))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStart))]
    [NotifyPropertyChangedFor(nameof(ShowStop))]
    private bool _isStarting;

    [ObservableProperty]
    private string? _serial;

    [ObservableProperty]
    private string? _tag;

    public EmulatorDeviceViewModel(AndroidVirtualDevice device, ICommand deleteCommand)
    {
        Name = device.Name;
        DeleteCommand = deleteCommand;
        Update(device);
    }

    /// <summary>The list's delete command, exposed here so the row's overflow menu (a flyout, which can't reach
    /// the list view model via the visual tree) can bind to it from the row's own DataContext.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>The AVD id — the immutable identity used to match rows across refreshes.</summary>
    public string Name { get; }

    /// <summary>Show the start affordance: stopped and not mid-launch.</summary>
    public bool ShowStart => !IsRunning && !IsStarting;

    /// <summary>Show the stop affordance: running and not mid-launch.</summary>
    public bool ShowStop => IsRunning && !IsStarting;

    /// <summary>Applies the latest service snapshot to this row, preserving its identity.</summary>
    public void Update(AndroidVirtualDevice device)
    {
        DisplayName = device.DisplayName;
        Tag = device.Tag;
        Serial = device.Serial;
        IsRunning = device.IsRunning;

        // Once adb reports the AVD running, the launch has finished — clear the transient state.
        if (device.IsRunning)
        {
            IsStarting = false;
        }
    }
}
