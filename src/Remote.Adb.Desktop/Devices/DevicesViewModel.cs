using System.Collections.ObjectModel;
using Remote.Adb.Core.Adb;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.Devices;

public sealed class DevicesViewModel : AutoRefreshingListViewModel
{
    // Background re-list cadence while the page is live, so a device plugged/unplugged or connected over the
    // network shows up without a manual refresh.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IDeviceService _deviceService;

    public DevicesViewModel(IDeviceService deviceService, INotificationService notifications, ITimerFactory timerFactory)
        : base(timerFactory, notifications, RefreshInterval)
    {
        _deviceService = deviceService;

        Devices.CollectionChanged += (_, _) => RaiseIsListEmptyChanged();
    }

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];

    protected override bool IsEmpty => Devices.Count == 0;

    protected override string LoadErrorTitle => "Couldn't list devices";

    protected override async Task LoadAsync() =>
        Devices.MergeBy(
            await _deviceService.ListAsync(),
            device => device.Serial,
            row => row.Serial,
            device => new DeviceRowViewModel(device),
            (row, device) => row.Update(device),
            row => row.DisplayName);
}
