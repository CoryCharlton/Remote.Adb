using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// The two-step "create AVD" wizard: pick a form factor + device + system image, tune the optional settings,
/// then create atomically on Finish (<c>avdmanager create</c> + config overrides). Nothing touches disk until
/// Finish, so cancelling leaves no AVD behind.
/// </summary>
public partial class CreateAvdViewModel : ViewModelBase, IDialogViewModel
{
    // Form factors in display order; only those present in the device catalog are shown.
    private static readonly FormFactorOption[] AllFormFactors =
    [
        new("phone", "Phone"),
        new("tablet", "Tablet"),
        new("wear", "Wear OS"),
        new("desktop", "Desktop"),
        new("tv", "TV"),
        new("automotive", "Automotive"),
        new("xr", "XR"),
    ];

    private static readonly ApiFilterOption AllApiLevels = new(null, "All API levels");

    private const string AllServices = "All services";

    private readonly IAvdProvisioningService _provisioning;
    private readonly IAvdConfigStore _store;
    private readonly IReadOnlyList<AvdField> _tunableFields;
    private IReadOnlyList<DeviceProfile> _allDevices = [];
    private IReadOnlyList<SystemImagePackage> _allImages = [];
    private string? _createdAvdId;
    private IReadOnlyList<DeviceProfile> _formFactorDevices = [];
    private IReadOnlyList<SystemImagePackage> _formFactorImages = [];
    private bool _suppressImageFilter;

    // 0 = device picker, 1 = configure (name + image + additional settings).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstStep))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private int _currentStep;

    // The configure step's inner tab: 0 = Device, 1 = Additional settings.
    [ObservableProperty]
    private int _configureTab;

    [ObservableProperty]
    private string _deviceSearch = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _frameHeight;

    [ObservableProperty]
    private double _frameWidth;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _noSystemImages;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ApiFilterOption? _selectedApiFilter = AllApiLevels;

    [ObservableProperty]
    private string _selectedServicesFilter = AllServices;

    [ObservableProperty]
    private DeviceProfile? _selectedDevice;

    [ObservableProperty]
    private FormFactorOption? _selectedFormFactor;

    [ObservableProperty]
    private SystemImagePackage? _selectedImage;

    [ObservableProperty]
    private bool _showObsolete;

    [ObservableProperty]
    private string? _statusMessage;

    public CreateAvdViewModel(IAvdProvisioningService provisioning, IAvdConfigStore store)
    {
        _provisioning = provisioning;
        _store = store;

        // The wizard's settings step is always editable, built from a blank config so every tunable shows.
        var built = AvdDetailFields.BuildTunable(new AvdConfiguration(IniParser.Parse(string.Empty), null));
        Groups = built.Groups;
        _tunableFields = built.Fields;

        foreach (var field in _tunableFields)
        {
            field.IsEditing = true;
        }

        foreach (var group in Groups)
        {
            group.IsEditing = true;
        }

        _ = LoadAsync();
    }

    /// <summary>Raised when the wizard wants to close; the argument is whether an AVD was created.</summary>
    public event Action<bool>? CloseRequested;

    public ObservableCollection<ApiFilterOption> ApiFilters { get; } = [];

    public ObservableCollection<DeviceProfile> Devices { get; } = [];

    public ObservableCollection<FormFactorOption> FormFactors { get; } = [];

    public IReadOnlyList<DetailGroup> Groups { get; }

    public ObservableCollection<SystemImagePackage> Images { get; } = [];

    public bool IsFirstStep => CurrentStep == 0;

    public bool IsLastStep => CurrentStep == 1;

    public ObservableCollection<string> ServicesFilters { get; } = [];

    private Dictionary<string, string> BuildOverrides(string avdId)
    {
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["avd.ini.displayname"] = string.IsNullOrWhiteSpace(DisplayName) ? avdId : DisplayName.Trim(),
        };

        foreach (var field in _tunableFields.Where(field => field.IsDirty && field.HasValue))
        {
            overrides[field.Key] = (field.Value ?? string.Empty).Trim();
        }

        return overrides;
    }

    // If the AVD was already created (but its config write failed below), cancelling still needs to refresh
    // the list so the new AVD shows up.
    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(_createdAvdId is not null);

    [RelayCommand]
    private async Task FinishAsync()
    {
        StatusMessage = null;

        if (!AvdValueConventions.IsValidAvdName(Name))
        {
            StatusMessage = "Enter a valid AVD name (letters, digits, '.', '_', '-').";
            CurrentStep = 0;
            return;
        }

        if (SelectedDevice is null)
        {
            StatusMessage = "Select a device.";
            CurrentStep = 0;
            return;
        }

        if (SelectedImage is null)
        {
            StatusMessage = "Select a system image.";
            CurrentStep = 0;
            return;
        }

        var valid = true;
        foreach (var field in _tunableFields)
        {
            if (!field.Validate())
            {
                valid = false;
            }
        }

        if (!valid)
        {
            StatusMessage = "Fix the highlighted settings before finishing.";
            CurrentStep = 1;
            return;
        }

        IsBusy = true;

        try
        {
            // Creating the AVD is the irreversible step; once it succeeds, remember it so retrying after a
            // failed config write below doesn't create it again and hit a name collision.
            if (_createdAvdId is null)
            {
                var avdId = Name.Trim();

                var result = await _provisioning.CreateAsync(avdId, SelectedImage.Package, SelectedDevice.Id);
                if (!result.Success)
                {
                    StatusMessage = result.Error ?? "avdmanager could not create the AVD. Check the SDK installation.";
                    return;
                }

                _createdAvdId = avdId;
            }

            // The display name and tunable overrides are persisted by the store; a null result means the
            // just-created AVD couldn't be located to write them. The create still succeeded, so surface the
            // partial failure rather than silently reporting success and dropping the user's settings.
            if (_store.Write(_createdAvdId, BuildOverrides(_createdAvdId)) is null)
            {
                StatusMessage = "The emulator was created, but its name and settings couldn't be saved. "
                    + "It will appear with default settings — edit it from the list, or click Finish to retry.";
                return;
            }

            CloseRequested?.Invoke(true);
        }
        catch (ProcessLaunchException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Phones/tablets take the general images; specialised form factors take their own.
    private static string ImageCategory(string? formFactor) =>
        formFactor is "phone" or "tablet" or null ? "general" : formFactor;

    private async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            _allImages = await _provisioning.ListInstalledImagesAsync();
            _allDevices = await _provisioning.ListDevicesAsync();

            if (_allDevices.Count == 0)
            {
                StatusMessage = "No device profiles found. A JDK may be required (set JAVA_HOME), "
                    + "or install the command-line tools.";
                return;
            }

            var present = _allDevices.Select(device => device.FormFactor).ToHashSet(StringComparer.Ordinal);
            foreach (var option in AllFormFactors.Where(option => present.Contains(option.Key)))
            {
                FormFactors.Add(option);
            }

            // Default to Phone; selecting a form factor fills the device + image lists.
            SelectedFormFactor = FormFactors.FirstOrDefault(option => option.Key == "phone")
                ?? FormFactors.FirstOrDefault();
        }
        catch (ProcessLaunchException exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Next() => CurrentStep = 1;

    // Show the device's defaults in the settings (currently RAM, which avdmanager seeds from the device),
    // baselined so they appear but aren't re-written unless the user changes them.
    partial void OnSelectedDeviceChanged(DeviceProfile? value)
    {
        SetFieldDefault("hw.ramSize", value?.RamMb?.ToString());
        UpdateFrame(value);
    }

    // Sizes the summary's device-frame illustration to the device's aspect ratio, scaled to fit the panel.
    private void UpdateFrame(DeviceProfile? device)
    {
        if (device?.ScreenWidth is not { } width || device.ScreenHeight is not { } height || width <= 0 || height <= 0)
        {
            FrameWidth = 0;
            FrameHeight = 0;
            return;
        }

        const double maxWidth = 150;
        const double maxHeight = 180;
        var scale = Math.Min(maxWidth / width, maxHeight / height);
        FrameWidth = Math.Round(width * scale);
        FrameHeight = Math.Round(height * scale);
    }

    partial void OnDeviceSearchChanged(string value) => ApplyDeviceFilters();

    partial void OnShowObsoleteChanged(bool value) => ApplyDeviceFilters();

    // Applies the "show obsolete" toggle and the name search to the current form factor's devices.
    private void ApplyDeviceFilters()
    {
        var search = DeviceSearch.Trim();

        Devices.Clear();
        foreach (var device in _formFactorDevices
                     .Where(device => ShowObsolete || !device.IsObsolete)
                     .Where(device => search.Length == 0 || device.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            Devices.Add(device);
        }

        SelectedDevice = Devices.FirstOrDefault(device => device.Id == "medium_phone")
            ?? Devices.FirstOrDefault();
    }

    // Selecting a form factor narrows both the device list and the compatible system images.
    partial void OnSelectedFormFactorChanged(FormFactorOption? value)
    {
        _formFactorDevices = _allDevices.Where(device => device.FormFactor == value?.Key).ToList();
        ApplyDeviceFilters();

        var imageCategory = ImageCategory(value?.Key);
        _formFactorImages = _allImages.Where(image => AvdCategories.Of(image.Tag) == imageCategory).ToList();
        RebuildImageFilters();
        ApplyImageFilters();
    }

    partial void OnSelectedApiFilterChanged(ApiFilterOption? value) => ApplyImageFilters();

    partial void OnSelectedServicesFilterChanged(string value) => ApplyImageFilters();

    // Applies the API + Services filters (and the form-factor narrowing) to the visible image list.
    private void ApplyImageFilters()
    {
        if (_suppressImageFilter)
        {
            return;
        }

        var images = _formFactorImages.AsEnumerable();
        if (SelectedApiFilter?.ApiLevel is { } apiLevel)
        {
            images = images.Where(image => image.ApiLevel == apiLevel);
        }

        if (SelectedServicesFilter != AllServices)
        {
            images = images.Where(image => image.Services == SelectedServicesFilter);
        }

        Images.Clear();
        foreach (var image in images)
        {
            Images.Add(image);
        }

        NoSystemImages = Images.Count == 0;
        SelectedImage = Images.FirstOrDefault();
    }

    [RelayCommand]
    private void Previous() => CurrentStep = 0;

    // Rebuilds the API/Services filter choices for the current form factor's images and resets them to "All".
    private void RebuildImageFilters()
    {
        _suppressImageFilter = true;

        ApiFilters.Clear();
        ApiFilters.Add(AllApiLevels);
        foreach (var api in _formFactorImages.Select(image => image.ApiLevel).Distinct().OrderByDescending(api => api))
        {
            ApiFilters.Add(new ApiFilterOption(api, $"API {api}"));
        }

        ServicesFilters.Clear();
        ServicesFilters.Add(AllServices);
        foreach (var services in _formFactorImages.Select(image => image.Services).Distinct().OrderBy(services => services, StringComparer.Ordinal))
        {
            ServicesFilters.Add(services);
        }

        SelectedApiFilter = AllApiLevels;
        SelectedServicesFilter = AllServices;

        _suppressImageFilter = false;
    }

    // Pre-fills a tunable field and rebaselines it, so the value shows as the default without counting as a
    // user change (and so it isn't redundantly written on Finish). A field the user has already edited is
    // left alone — switching devices (or an incidental search/filter that reassigns SelectedDevice) must not
    // silently overwrite a value they typed.
    private void SetFieldDefault(string key, string? value)
    {
        var field = _tunableFields.FirstOrDefault(field => field.Key == key);
        if (field is null || field.IsDirty)
        {
            return;
        }

        field.Value = value ?? string.Empty;
        field.Commit();
    }
}
