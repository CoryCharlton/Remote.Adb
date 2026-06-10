using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;

namespace Remote.Adb.Desktop.Emulators;

/// <inheritdoc />
public sealed class AvdCreateDialog : IAvdCreateDialog
{
    private readonly INotificationService _notifications;
    private readonly IAvdProvisioningService _provisioning;
    private readonly IAvdConfigStore _store;

    public AvdCreateDialog(IAvdProvisioningService provisioning, IAvdConfigStore store, INotificationService notifications)
    {
        _provisioning = provisioning;
        _store = store;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public Task<bool> ShowAsync() =>
        DialogHost.ShowAsync<CreateAvdWizardWindow>(new CreateAvdViewModel(_provisioning, _store, _notifications));
}
