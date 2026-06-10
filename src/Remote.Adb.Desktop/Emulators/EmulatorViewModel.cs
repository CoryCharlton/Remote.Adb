using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;

namespace Remote.Adb.Desktop.Emulators;

public partial class EmulatorViewModel : ViewModelBase, IActivatable
{
    // A launched AVD takes a while to register with adb. Poll for it to come up, and stop
    // waiting after the timeout so a row can't get stuck in the "starting" state forever.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    private readonly IAvdConfigStore _configStore;
    private readonly IConfirmDialog _confirmDialog;
    private readonly IAvdCreateDialog _createDialog;
    private readonly EmulatorDetailsViewModelFactory _detailsFactory;
    private readonly IEmulatorService _emulatorService;
    private readonly INotificationService _notifications;
    private readonly IAvdProvisioningService _provisioning;
    private bool _hasLoaded;
    private bool _loadFailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    private EmulatorDetailsViewModel? _selectedDetail;

    public EmulatorViewModel(
        IEmulatorService emulatorService,
        IAvdConfigStore configStore,
        IAvdCreateDialog createDialog,
        EmulatorDetailsViewModelFactory detailsFactory,
        IAvdProvisioningService provisioning,
        IConfirmDialog confirmDialog,
        INotificationService notifications)
    {
        _emulatorService = emulatorService;
        _configStore = configStore;
        _createDialog = createDialog;
        _detailsFactory = detailsFactory;
        _provisioning = provisioning;
        _confirmDialog = confirmDialog;
        _notifications = notifications;

        Emulators.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsListEmpty));
    }

    public ObservableCollection<EmulatorDeviceViewModel> Emulators { get; } = [];

    // The full-page empty state shows only once a load has settled with nothing to show (not while busy, and not
    // masking a load error).
    public bool IsListEmpty => !IsBusy && !_loadFailed && Emulators.Count == 0;

    // Opens the create wizard; if an AVD was created, refresh so it shows up in the list.
    [RelayCommand]
    private async Task CreateAsync()
    {
        if (await _createDialog.ShowAsync())
        {
            await RefreshAsync();
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
            await RefreshAsync();
        }
        catch (ProcessLaunchException exception)
        {
            NotifyError("Delete failed", exception.Message);
        }
    }

    // Reconciles the rows with the latest service snapshot in place, so selection and the
    // transient "starting" state survive a refresh instead of being thrown away.
    private void Merge(IReadOnlyList<AndroidVirtualDevice> devices)
    {
        foreach (var device in devices)
        {
            var existing = Emulators.FirstOrDefault(e => e.Name == device.Name);

            if (existing is null)
            {
                Emulators.Add(new EmulatorDeviceViewModel(device, DeleteCommand));
            }
            else
            {
                existing.Update(device);
            }
        }

        for (var i = Emulators.Count - 1; i >= 0; i--)
        {
            if (devices.All(d => d.Name != Emulators[i].Name))
            {
                Emulators.RemoveAt(i);
            }
        }

        var ordered = Emulators.OrderBy(emulator => emulator.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var target = 0; target < ordered.Count; target++)
        {
            var current = Emulators.IndexOf(ordered[target]);
            if (current != target)
            {
                Emulators.Move(current, target);
            }
        }
    }

    // Transient failures surface as an auto-dismissing error toast (not a persistent inline label).
    private void NotifyError(string title, string message) =>
        _notifications.Show(title, message, NotificationSeverity.Error);

    /// <summary>Loads the emulator list the first time the page is selected.</summary>
    public async Task OnActivatedAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            Merge(await _emulatorService.ListAsync());
            _loadFailed = false;
        }
        catch (ProcessLaunchException exception)
        {
            _loadFailed = true;
            NotifyError("Couldn't load emulators", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

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
            await RefreshAsync();
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
