using System.Threading.Tasks;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Emulators;

/// <inheritdoc />
public sealed class AvdCreateDialog : IAvdCreateDialog
{
    private readonly IAvdProvisioningService _provisioning;
    private readonly IAvdConfigStore _store;

    public AvdCreateDialog(IAvdProvisioningService provisioning, IAvdConfigStore store)
    {
        _provisioning = provisioning;
        _store = store;
    }

    /// <inheritdoc />
    public Task<bool> ShowAsync() =>
        DialogHost.ShowAsync<CreateAvdWizardWindow>(new CreateAvdViewModel(_provisioning, _store));
}
