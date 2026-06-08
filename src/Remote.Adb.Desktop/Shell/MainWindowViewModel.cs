using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DrawerLength))]
    private bool _isRail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentScreen))]
    private NavigationDestination? _selectedDestination;

    public MainWindowViewModel(
        EmulatorViewModel emulator,
        DevicesViewModel devices,
        TunnelViewModel tunnel,
        SettingsViewModel settings)
    {
        Destinations =
        [
            new NavigationDestination("Emulators", "IconAndroidLogo", emulator),
            new NavigationDestination("Devices", "IconDeviceMobile", devices),
            new NavigationDestination("Tunnel", "IconPlugsConnected", tunnel),
            new NavigationDestination("Settings", "IconGear", settings),
        ];

        // Set via the property (not the field) so the initial destination is activated too.
        SelectedDestination = Destinations[0];
    }

    public ViewModelBase CurrentScreen => (SelectedDestination ?? Destinations[0]).Screen;

    public IReadOnlyList<NavigationDestination> Destinations { get; }

    // Labeled drawer (260) collapses to the icon rail (80).
    public double DrawerLength => IsRail ? 80 : 260;

    // Let a screen run its activation logic (e.g. load its data) when it becomes selected.
    // Fire-and-forget is fine: activation handlers surface their own errors to the screen.
    partial void OnSelectedDestinationChanged(NavigationDestination? value)
    {
        if (value?.Screen is IActivatable activatable)
        {
            _ = activatable.OnActivatedAsync();
        }
    }

    [RelayCommand]
    private void ToggleRail()
    {
        IsRail = !IsRail;
    }
}
