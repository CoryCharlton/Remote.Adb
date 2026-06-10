using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Diagnostics;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISdkDiagnostics _diagnostics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DrawerLength))]
    private bool _isRail;

    private readonly INotificationService _notifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentScreen))]
    private NavigationDestination? _selectedDestination;

    public MainWindowViewModel(
        EmulatorViewModel emulator,
        DevicesViewModel devices,
        TunnelViewModel tunnel,
        SettingsViewModel settings,
        ISdkDiagnostics diagnostics,
        INotificationService notifications)
    {
        _diagnostics = diagnostics;
        _notifications = notifications;

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

    [RelayCommand]
    private void GoToSettings()
    {
        SelectedDestination = Destinations.First(destination => destination.Label == "Settings");
    }

    // Let a screen run its activation logic (e.g. load its data) when it becomes selected.
    // Fire-and-forget is fine: activation handlers surface their own errors to the screen.
    partial void OnSelectedDestinationChanged(NavigationDestination? value)
    {
        if (value?.Screen is IActivatable activatable)
        {
            _ = activatable.OnActivatedAsync();
        }
    }

    // Surface any unconfigured-tool diagnostics (SDK / JDK) as one persistent toast each; clicking opens Settings.
    // Called by the shell once its notification sink is attached, rather than as a constructor side effect.
    public void RaiseStartupDiagnostics()
    {
        foreach (var diagnostic in _diagnostics.Evaluate())
        {
            var severity = diagnostic.Severity == DiagnosticSeverity.Error
                ? NotificationSeverity.Error
                : NotificationSeverity.Warning;

            _notifications.Show(diagnostic.Title, diagnostic.Message, severity, TimeSpan.Zero, () => GoToSettingsCommand.Execute(null));
        }
    }

    [RelayCommand]
    private void ToggleRail()
    {
        IsRail = !IsRail;
    }
}
