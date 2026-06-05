namespace Remote.Adb.Core.Emulators;

/// <summary>
/// An Android Virtual Device (emulator) and its current running state.
/// </summary>
/// <param name="Name">The AVD id as reported by <c>emulator -list-avds</c> (used to start the AVD).</param>
/// <param name="DisplayName">The friendly name (<c>avd.ini.displayname</c>); falls back to <paramref name="Name"/>.</param>
/// <param name="Tag">The device category (<c>tag.displaynames</c>, e.g. "Google TV"); <see langword="null"/> if unknown.</param>
/// <param name="IsRunning">Whether an emulator instance for this AVD is currently running.</param>
/// <param name="Serial">The adb serial (e.g. <c>emulator-5554</c>) when running; otherwise <see langword="null"/>.</param>
public sealed record AndroidVirtualDevice(string Name, string DisplayName, string? Tag, bool IsRunning, string? Serial);
