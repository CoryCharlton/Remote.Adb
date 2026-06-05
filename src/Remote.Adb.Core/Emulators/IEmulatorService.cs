
namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Lists, starts, and stops Android emulators (AVDs).
/// </summary>
public interface IEmulatorService
{
    /// <summary>
    /// Lists the available AVDs, annotated with whether each is currently running and its serial.
    /// </summary>
    Task<IReadOnlyList<AndroidVirtualDevice>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches the emulator for the given AVD name. Does not wait for it to finish booting.
    /// </summary>
    Task StartAsync(string avdName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the running emulator with the given adb serial (e.g. <c>emulator-5554</c>).
    /// </summary>
    Task StopAsync(string serial, CancellationToken cancellationToken = default);
}
