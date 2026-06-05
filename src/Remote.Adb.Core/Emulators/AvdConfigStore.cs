using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Remote.Adb.Core.Emulators;

/// <inheritdoc />
public sealed class AvdConfigStore : IAvdConfigStore
{
    private readonly ILogger<AvdConfigStore> _logger;

    public AvdConfigStore(ILogger<AvdConfigStore> logger)
    {
        _logger = logger;
    }

    // Walks the AVD home, yielding each .avd folder's parsed config.ini plus its sibling <id>.ini. Shared by
    // Locate (which filters by AvdId) and ReadAll, so the directory walk and parsing live in one place.
    private IEnumerable<Located> EnumerateAll()
    {
        var avdHome = AvdHome.Resolve();
        if (avdHome is null)
        {
            _logger.LogDebug("AVD home not found.");
            yield break;
        }

        foreach (var avdDirectory in Directory.EnumerateDirectories(avdHome, "*.avd"))
        {
            var configPath = Path.Combine(avdDirectory, "config.ini");
            if (!File.Exists(configPath))
            {
                continue;
            }

            Located? located;
            try
            {
                var config = IniParser.Parse(File.ReadAllText(configPath));

                // The sibling metadata file (path=/target=) is named after the .avd folder.
                var siblingPath = Path.Combine(avdHome, Path.GetFileNameWithoutExtension(avdDirectory) + ".ini");
                var sibling = File.Exists(siblingPath) ? IniParser.Parse(File.ReadAllText(siblingPath)) : null;

                located = new Located(configPath, new AvdConfiguration(config, sibling));
            }
            catch (IOException exception)
            {
                _logger.LogDebug(exception, "Could not read {ConfigPath}", configPath);
                located = null;
            }

            if (located is not null)
            {
                yield return located;
            }
        }
    }

    // Finds the AVD whose config.ini declares the given AvdId (the .avd folder name need not match it).
    private Located? Locate(string avdId)
    {
        foreach (var located in EnumerateAll())
        {
            if (string.Equals(located.Configuration.AvdId, avdId, StringComparison.Ordinal))
            {
                return located;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public AvdConfiguration? Read(string avdId) => Locate(avdId)?.Configuration;

    /// <inheritdoc />
    public IReadOnlyList<AvdConfiguration> ReadAll()
    {
        var configurations = new List<AvdConfiguration>();

        foreach (var located in EnumerateAll())
        {
            configurations.Add(located.Configuration);
        }

        return configurations;
    }

    /// <inheritdoc />
    public AvdConfiguration? Write(
        string avdId,
        IReadOnlyDictionary<string, string> changes,
        IReadOnlyCollection<string>? removals = null)
    {
        var located = Locate(avdId);
        if (located is null)
        {
            _logger.LogDebug("No AVD found with id {AvdId}; nothing written.", avdId);
            return null;
        }

        if (changes.Count == 0 && (removals is null || removals.Count == 0))
        {
            return located.Configuration;
        }

        try
        {
            var text = AvdConfigWriter.Write(located.Configuration.Config, changes, removals);
            File.WriteAllText(located.ConfigPath, text);

            // Return the freshly written state directly rather than re-reading. A re-read would re-scan the
            // AVD home and could spuriously fail (a concurrent rename/delete), reporting a successful write as
            // a failure; the sibling .ini isn't touched by this write, so reuse it as-is.
            return new AvdConfiguration(IniParser.Parse(text), located.Configuration.Sibling);
        }
        catch (IOException exception)
        {
            _logger.LogDebug(exception, "Could not write {ConfigPath}", located.ConfigPath);
            return null;
        }
    }

    private sealed record Located(string ConfigPath, AvdConfiguration Configuration);
}
