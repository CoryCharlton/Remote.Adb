namespace Remote.Adb.Core.Emulators;

/// <inheritdoc />
public sealed class AvdCatalog : IAvdCatalog
{
    private readonly IAvdConfigStore _store;

    public AvdCatalog(IAvdConfigStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, AvdMetadata> Read()
    {
        var catalog = new Dictionary<string, AvdMetadata>(StringComparer.Ordinal);

        // One walk of the AVD home (in the store), projected to the lightweight list metadata.
        foreach (var configuration in _store.ReadAll())
        {
            // Skip config.ini files that aren't a usable AVD (no AvdId).
            if (string.IsNullOrEmpty(configuration.AvdId))
            {
                continue;
            }

            catalog[configuration.AvdId] = configuration.ToMetadata();
        }

        return catalog;
    }
}
