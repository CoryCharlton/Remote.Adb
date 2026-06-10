using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.Emulators;

public partial class EmulatorViewModel : AutoRefreshingListViewModel
{
    // A launched AVD takes a while to register with adb. Poll for it to come up, and stop
    // waiting after the timeout so a row can't get stuck in the "starting" state forever.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // Background re-list cadence while the page is live, so external start/stop/create shows up without a manual
    // refresh. Listing shells out to adb/emulator, so keep it modest.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    private readonly IAvdConfigStore _configStore;
    private readonly IConfirmDialog _confirmDialog;
    private readonly IAvdCreateDialog _createDialog;
    private readonly EmulatorDetailsViewModelFactory _detailsFactory;
    private readonly IEmulatorService _emulatorService;
    private readonly IAvdProvisioningService _provisioning;

    [ObservableProperty]
    private EmulatorDetailsViewModel? _selectedDetail;

    public EmulatorViewModel(
        IEmulatorService emulatorService,
        IAvdConfigStore configStore,
        IAvdCreateDialog createDialog,
        EmulatorDetailsViewModelFactory detailsFactory,
        IAvdProvisioningService provisioning,
        IConfirmDialog confirmDialog,
        INotificationService notifications,
        ITimerFactory timerFactory)
        : base(timerFactory, notifications, RefreshInterval)
    {
        _emulatorService = emulatorService;
        _configStore = configStore;
        _createDialog = createDialog;
        _detailsFactory = detailsFactory;
        _provisioning = provisioning;
        _confirmDialog = confirmDialog;

        Emulators.CollectionChanged += (_, _) => RaiseIsListEmptyChanged();
    }

    public ObservableCollection<EmulatorDeviceViewModel> Emulators { get; } = [];

    protected override bool IsEmpty => Emulators.Count == 0;

    protected override string LoadErrorTitle => "Couldn't load emulators";

    // Don't fight an in-flight start poll: skip the background tick while a row is mid-launch.
    protected override bool SkipBackgroundTick => Emulators.Any(emulator => emulator.IsStarting);

    // Opens the create wizard; if an AVD was created, refresh so it shows up in the list.
    [RelayCommand]
    private async Task CreateAsync()
    {
        if (await _createDialog.ShowAsync())
        {
            await RefreshAsync(notifyOnError: true);
        }
    }

    // Confirms, then deletes the AVD via avdmanager and refreshes the list. The row's menu item is disabled
    // while the AVD is running; this re-checks for safety (deleting a running AVD's files would fail).
    [RelayCommand]
    private async Task DeleteAsync(EmulatorDeviceViewModel? device)
    {
        if (device is null || device.IsRunning)
        {
            return;
        }

        var confirmed = await _confirmDialog.ConfirmAsync(
            "Delete emulator?",
            $"Delete '{device.DisplayName}'? This permanently removes the AVD and its data.",
            "Delete");

        if (!confirmed)
        {
            return;
        }

        try
        {
            if (!await _provisioning.DeleteAsync(device.Name))
            {
                NotifyError("Delete failed", $"Could not delete '{device.DisplayName}'.");
                return;
            }

            SelectedDetail = null;
            await RefreshAsync(notifyOnError: true);
        }
        catch (ProcessLaunchException exception)
        {
            NotifyError("Delete failed", exception.Message);
        }
    }

    protected override async Task LoadAsync() => Merge(await _emulatorService.ListAsync());

    // Reconciles the rows with the latest service snapshot in place, so selection and the
    // transient "starting" state survive a refresh instead of being thrown away.
    private void Merge(IReadOnlyList<AndroidVirtualDevice> devices) =>
        Emulators.MergeBy(
            devices,
            device => device.Name,
            row => row.Name,
            device => new EmulatorDeviceViewModel(device, DeleteCommand),
            (row, device) => row.Update(device),
            row => row.DisplayName);

    // Transient failures surface as an auto-dismissing error toast (not a persistent inline label).
    private void NotifyError(string title, string message) =>
        Notifications.Show(title, message, NotificationSeverity.Error);

    [RelayCommand]
    private async Task StartAsync(EmulatorDeviceViewModel? device)
    {
        if (device is null || device.IsRunning || device.IsStarting)
        {
            return;
        }

        device.IsStarting = true;

        try
        {
            await _emulatorService.StartAsync(device.Name);
            await WaitUntilRunningAsync(device);
        }
        catch (ProcessLaunchException exception)
        {
            NotifyError("Couldn't start emulator", exception.Message);
        }
        finally
        {
            device.IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync(EmulatorDeviceViewModel? device)
    {
        if (device?.Serial is null)
        {
            return;
        }

        try
        {
            await _emulatorService.StopAsync(device.Serial);
            await RefreshAsync(notifyOnError: true);
        }
        catch (ProcessLaunchException exception)
        {
            NotifyError("Couldn't stop emulator", exception.Message);
        }
    }

    // Opens the read-only details pane for a row, loading its full config.ini via the store. The detail
    // view model gets a Back callback that clears SelectedDetail, returning the screen to the list.
    [RelayCommand]
    private void ViewDetails(EmulatorDeviceViewModel? device)
    {
        if (device is null)
        {
            return;
        }

        var configuration = _configStore.Read(device.Name);
        if (configuration is null)
        {
            NotifyError("Details unavailable", $"No configuration found for {device.DisplayName}.");
            return;
        }

        SelectedDetail = _detailsFactory(configuration, () => SelectedDetail = null);
    }

    // Polls adb until the just-launched AVD registers as running (flipping its row out of the
    // "starting" state), or gives up after StartTimeout.
    private async Task WaitUntilRunningAsync(EmulatorDeviceViewModel device)
    {
        var deadline = DateTime.UtcNow + StartTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            var devices = await _emulatorService.ListAsync();
            Merge(devices);

            if (devices.Any(d => d.Name == device.Name && d.IsRunning))
            {
                return;
            }
        }
    }
}
