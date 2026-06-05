
namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Reads AVD metadata (friendly name, device tag) from the local AVD <c>config.ini</c> files.
/// </summary>
public interface IAvdCatalog
{
    /// <summary>
    /// Reads the AVD home and returns metadata keyed by <c>AvdId</c>. Returns an empty map if the AVD
    /// home can't be located. Re-reads on each call so newly created AVDs are picked up.
    /// </summary>
    IReadOnlyDictionary<string, AvdMetadata> Read();
}
