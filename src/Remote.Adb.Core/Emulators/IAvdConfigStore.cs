namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Reads and writes a single AVD's full configuration. Complements <see cref="IAvdCatalog"/>, which only
/// builds the lightweight list metadata for every AVD at once.
/// </summary>
public interface IAvdConfigStore
{
    /// <summary>
    /// Reads the full configuration for the AVD with the given <paramref name="avdId"/> (its
    /// <c>config.ini</c> plus the sibling <c>&lt;id&gt;.ini</c>), or <see langword="null"/> if none matches.
    /// </summary>
    AvdConfiguration? Read(string avdId);

    /// <summary>
    /// Reads the full configuration of every AVD under the AVD home (a single walk of the <c>.avd</c>
    /// folders). Returns an empty list if the AVD home can't be located.
    /// </summary>
    IReadOnlyList<AvdConfiguration> ReadAll();

    /// <summary>
    /// Applies <paramref name="changes"/> (set/append) and <paramref name="removals"/> (drop) to the AVD's
    /// <c>config.ini</c> — preserving every untouched line — writes it back, and returns the resulting
    /// configuration, or <see langword="null"/> if no AVD matches <paramref name="avdId"/> or the write fails.
    /// </summary>
    AvdConfiguration? Write(
        string avdId,
        IReadOnlyDictionary<string, string> changes,
        IReadOnlyCollection<string>? removals = null);
}
