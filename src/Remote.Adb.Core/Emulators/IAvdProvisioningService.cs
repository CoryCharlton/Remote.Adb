namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Creates and deletes AVDs via <c>avdmanager</c>, and enumerates the installed system images and device
/// profiles a new AVD can be built from. The tuned <c>config.ini</c> overrides are applied separately through
/// <see cref="IAvdConfigStore"/> after creation.
/// </summary>
public interface IAvdProvisioningService
{
    /// <summary>
    /// Creates an AVD (<c>avdmanager create avd</c>), returning whether it succeeded along with the tool's own
    /// message on failure.
    /// </summary>
    Task<AvdOperationResult> CreateAsync(string avdId, string systemImagePackage, string device, CancellationToken cancellationToken = default);

    /// <summary>Deletes an AVD (<c>avdmanager delete avd</c>). Returns whether it succeeded.</summary>
    Task<bool> DeleteAsync(string avdId, CancellationToken cancellationToken = default);

    /// <summary>The installed system images (scanned from the SDK root), newest API first.</summary>
    Task<IReadOnlyList<SystemImagePackage>> ListInstalledImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>The device profiles from <c>avdmanager list device</c>.</summary>
    Task<IReadOnlyList<DeviceProfile>> ListDevicesAsync(CancellationToken cancellationToken = default);
}
