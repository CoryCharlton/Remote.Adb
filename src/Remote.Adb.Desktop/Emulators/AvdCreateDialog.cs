using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Emulators;

/// <inheritdoc />
public sealed class AvdCreateDialog : IAvdCreateDialog
{
    private readonly Func<CreateAvdViewModel> _viewModelFactory;

    public AvdCreateDialog(Func<CreateAvdViewModel> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    /// <inheritdoc />
    public Task<bool> ShowAsync() => DialogHost.ShowAsync<CreateAvdWizardWindow>(_viewModelFactory());
}
