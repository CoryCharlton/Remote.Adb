using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>Creates an <see cref="EmulatorDetailsViewModel"/> for a specific AVD, with its service dependency
/// resolved from DI and the per-call runtime arguments (the configuration and back callback) supplied explicitly.</summary>
public delegate EmulatorDetailsViewModel EmulatorDetailsViewModelFactory(AvdConfiguration configuration, Action back);
