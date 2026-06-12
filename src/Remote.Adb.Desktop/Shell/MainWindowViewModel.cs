using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Diagnostics;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private IActivatable? _activeScreen;
    private readonly ISdkDiagnostics _diagnostics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DrawerLength))]
    private bool _isRail;

    private readonly INotificationService _notifications;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentScreen))]
    private NavigationDestination? _selectedDestination;

    private bool _windowActive = true;

    public MainWindowViewModel(
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

    partial void OnSelectedDestinationChanged(NavigationDestination? value)
    {
        UpdateActiveScreen();
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

    // Driven by the window (focus / minimize); a screen is only live while the window is in front.
    public void SetWindowActive(bool isActive)
    {
        if (_windowActive == isActive)
        {
            return;
        }

        _windowActive = isActive;
        UpdateActiveScreen();
    }

    [RelayCommand]
    private void ToggleRail()
    {
        IsRail = !IsRail;
    }

    // A screen is live when it is the selected destination and the window is in front. Deactivate the previously
    // live screen and activate the new one whenever either input changes. Activation is fire-and-forget — handlers
    // surface their own errors to the screen.
    private void UpdateActiveScreen()
    {
        var desired = _windowActive ? SelectedDestination?.Screen as IActivatable : null;
        if (ReferenceEquals(desired, _activeScreen))
        {
            return;
        }

        _activeScreen?.OnDeactivated();
        _activeScreen = desired;
        _ = desired?.OnActivatedAsync();
    }
}
