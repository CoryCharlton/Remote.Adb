using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.Devices;

/// <summary>
/// The unified device page: lists virtual devices (AVDs, running or stopped) and physical/wireless devices in
/// one auto-refreshing list. Virtual rows carry start/stop/delete/details; physical rows are read-only with a
/// connection indicator.
/// </summary>
public partial class DevicesViewModel : AutoRefreshingListViewModel
{
    private const string EmulatorSerialPrefix = "emulator-";

    // A launched AVD takes a while to register with adb. Poll for it to come up, and stop waiting after the
    // timeout so a row can't get stuck in the "starting" state forever.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // Background re-list cadence while the page is live, so external start/stop/create or a device plugged/
    // unplugged shows up without a manual refresh. Listing shells out to adb/emulator, so keep it modest.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    private readonly IAvdConfigStore _configStore;
    private readonly IConfirmDialog _confirmDialog;
    private readonly IAvdCreateDialog _createDialog;
    private readonly AvdDetailsViewModelFactory _detailsFactory;
    private readonly IDeviceService _deviceService;
    private readonly IEmulatorService _emulatorService;
    private readonly IAvdProvisioningService _provisioning;

    [ObservableProperty]
    private AvdDetailsViewModel? _selectedDetail;

    public DevicesViewModel(
        IEmulatorService emulatorService,
        IDeviceService deviceService,
        IAvdConfigStore configStore,
        IAvdCreateDialog createDialog,
        AvdDetailsViewModelFactory detailsFactory,
        IAvdProvisioningService provisioning,
        IConfirmDialog confirmDialog,
        INotificationService notifications,
        ITimerFactory timerFactory)
        : base(timerFactory, notifications, RefreshInterval)
    {
        _emulatorService = emulatorService;
        _deviceService = deviceService;
        _configStore = configStore;
        _createDialog = createDialog;
        _detailsFactory = detailsFactory;
        _provisioning = provisioning;
        _confirmDialog = confirmDialog;

        Devices.CollectionChanged += (_, _) => RaiseIsListEmptyChanged();
    }

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];

    protected override bool IsEmpty => Devices.Count == 0;

    protected override string LoadErrorTitle => "Couldn't list devices";

    // Don't fight an in-flight start poll: skip the background tick while an AVD is mid-launch.
    protected override bool SkipBackgroundTick => Devices.Any(device => device is { IsVirtual: true, IsStarting: true });

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
    private async Task DeleteAsync(DeviceRowViewModel? device)
    {
        if (device is null || !device.IsVirtual || device.IsRunning)
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

    // Lists AVDs and adb devices concurrently, drops the running emulators from the adb list (they're already
    // represented by AVD rows), then reconciles each source into its own rows and reorders the whole list.
    protected override async Task LoadAsync()
    {
        var avdTask = _emulatorService.ListAsync();
        var deviceTask = _deviceService.ListAsync();
        await Task.WhenAll(avdTask, deviceTask);

        MergeVirtual(avdTask.Result);
        MergePhysical(deviceTask.Result);
        Devices.SortBy(device => device.SortKey);
    }

    private void MergePhysical(IReadOnlyList<AdbDevice> devices)
    {
        var physical = devices.Where(device => !device.Serial.StartsWith(EmulatorSerialPrefix, StringComparison.Ordinal)).ToList();

        Devices.MergeByKind(
            physical,
            device => "dev:" + device.Serial,
            row => row.IdentityKey,
            row => row.IsPhysical,
            device => new DeviceRowViewModel(device),
            (row, device) => row.Update(device));
    }

    // Reconciles the virtual rows with the latest AVD snapshot in place, so selection and the transient
    // "starting" state survive a refresh instead of being thrown away. Leaves physical rows untouched.
    private void MergeVirtual(IReadOnlyList<AndroidVirtualDevice> devices) =>
        Devices.MergeByKind(
            devices,
            device => "avd:" + device.Name,
            row => row.IdentityKey,
            row => row.IsVirtual,
            device => new DeviceRowViewModel(device, DeleteCommand),
            (row, device) => row.Update(device));

    // Transient failures surface as an auto-dismissing error toast (not a persistent inline label).
    private void NotifyError(string title, string message) =>
        Notifications.Show(title, message, NotificationSeverity.Error);

    [RelayCommand]
    private async Task StartAsync(DeviceRowViewModel? device)
    {
        if (device is null || !device.IsVirtual || device.IsRunning || device.IsStarting)
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
    private async Task StopAsync(DeviceRowViewModel? device)
    {
        if (device?.Serial is null || !device.IsVirtual)
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

    // Opens the read-only details pane for an AVD row, loading its full config.ini via the store. The detail
    // view model gets a Back callback that clears SelectedDetail, returning the screen to the list.
    [RelayCommand]
    private void ViewDetails(DeviceRowViewModel? device)
    {
        if (device is null || !device.IsVirtual)
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

    // Polls adb until the just-launched AVD registers as running (flipping its row out of the "starting"
    // state), or gives up after StartTimeout. Merges only the virtual rows so physical rows are preserved.
    private async Task WaitUntilRunningAsync(DeviceRowViewModel device)
    {
        var deadline = DateTime.UtcNow + StartTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval);

            var devices = await _emulatorService.ListAsync();
            MergeVirtual(devices);
            Devices.SortBy(row => row.SortKey);

            if (devices.Any(d => d.Name == device.Name && d.IsRunning))
            {
                return;
            }
        }
    }
}
