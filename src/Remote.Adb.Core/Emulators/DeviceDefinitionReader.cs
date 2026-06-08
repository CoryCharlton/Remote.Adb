using System.IO.Compression;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Locates and reads the Android device definitions: the built-in catalog embedded in the command-line
/// tools' <c>sdklib</c> jars (<c>com/android/sdklib/devices/*.xml</c>), the legacy loose
/// <c>tools/lib/devices.xml</c>, and the user's <c>devices.xml</c>. Returns the merged, parsed list with
/// screen specs — far richer than <c>avdmanager list device</c>. Best-effort and I/O-tolerant.
/// </summary>
public static class DeviceDefinitionReader
{
    /// <summary>Reads and merges every available device-definition source under <paramref name="sdkRoot"/>.</summary>
    public static IReadOnlyList<DeviceProfile> Read(string? sdkRoot)
    {
        var devices = new List<DeviceProfile>();

        // User-defined devices override the built-ins.
        if (ReadFile(UserDevicesXmlPath()) is { } userXml)
        {
            devices.AddRange(DeviceDefinitionParser.Parse(userXml));
        }

        if (!string.IsNullOrWhiteSpace(sdkRoot))
        {
            // The current built-in catalog: device resources embedded in the cmdline-tools sdklib jars (the
            // same source avdmanager/Android Studio read).
            var hadEmbedded = false;
            foreach (var xml in EmbeddedDeviceXml(sdkRoot))
            {
                hadEmbedded = true;
                devices.AddRange(DeviceDefinitionParser.Parse(xml));
            }

            // Only fall back to the legacy loose tools/lib catalog when the modern one is absent — otherwise
            // it just duplicates devices with worse (name-as-id) entries.
            if (!hadEmbedded && ReadFile(Path.Combine(sdkRoot, "tools", "lib", "devices.xml")) is { } legacyXml)
            {
                devices.AddRange(DeviceDefinitionParser.Parse(legacyXml));
            }
        }

        // Dedupe by id, keeping the first occurrence (user override → built-ins).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return devices.Where(device => seen.Add(device.Id)).ToList();
    }

    private static IEnumerable<string> EmbeddedDeviceXml(string sdkRoot)
    {
        foreach (var jar in SdkLibJars(sdkRoot))
        {
            foreach (var xml in ReadDeviceEntries(jar))
            {
                yield return xml;
            }
        }
    }

    private static bool IsDeviceXmlEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && entry.FullName.Contains("/devices/", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ReadDeviceEntries(string jar)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(jar);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            yield break;
        }

        using (archive)
        {
            foreach (var entry in archive.Entries.Where(IsDeviceXmlEntry))
            {
                string xml;
                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    xml = reader.ReadToEnd();
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException)
                {
                    continue;
                }

                yield return xml;
            }
        }
    }

    private static string? ReadFile(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    // The cmdline-tools modules live in lib/sdklib/*.jar (e.g. sdklib.core.jar). The device resources are in
    // one of them; scan them all rather than hard-coding a jar name that varies across SDK versions.
    private static IEnumerable<string> SdkLibJars(string sdkRoot)
    {
        var cmdlineTools = Path.Combine(sdkRoot, "cmdline-tools");
        if (!Directory.Exists(cmdlineTools))
        {
            yield break;
        }

        foreach (var versionDirectory in Directory.EnumerateDirectories(cmdlineTools).OrderDescending())
        {
            var sdkLibDirectory = Path.Combine(versionDirectory, "lib", "sdklib");
            if (!Directory.Exists(sdkLibDirectory))
            {
                continue;
            }

            string[] jars;
            try
            {
                jars = Directory.GetFiles(sdkLibDirectory, "*.jar");
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var jar in jars)
            {
                yield return jar;
            }
        }
    }

    private static string? UserDevicesXmlPath()
    {
        var candidates = new[]
        {
            EnvPath("ANDROID_USER_HOME", "devices.xml"),
            EnvPath("ANDROID_SDK_HOME", ".android", "devices.xml"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "devices.xml"),
        };

        return candidates.FirstOrDefault(candidate => candidate is not null && File.Exists(candidate));
    }

    private static string? EnvPath(string variable, params string[] parts)
    {
        var root = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(new[] { root }.Concat(parts).ToArray());
    }
}
