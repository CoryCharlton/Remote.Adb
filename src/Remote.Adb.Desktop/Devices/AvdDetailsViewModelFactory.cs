using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Desktop.Devices;

/// <summary>Creates an <see cref="AvdDetailsViewModel"/> for a specific AVD, with its service dependency
/// resolved from DI and the per-call runtime arguments (the configuration and back callback) supplied explicitly.</summary>
public delegate AvdDetailsViewModel AvdDetailsViewModelFactory(AvdConfiguration configuration, Action back);
