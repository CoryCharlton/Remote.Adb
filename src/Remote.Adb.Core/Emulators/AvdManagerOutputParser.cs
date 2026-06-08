namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Parses <c>avdmanager</c> text output. Pure and I/O-free, mirroring <see cref="AdbOutputParser"/> /
/// <see cref="EmulatorOutputParser"/>.
/// </summary>
public static class AvdManagerOutputParser
{
    /// <summary>
    /// Parses <c>avdmanager list device</c> into device profiles. Each block looks like
    /// <c>id: 0 or "pixel_6"</c> / <c>Name: Pixel 6</c> / <c>OEM : Google</c>, separated by a dashed line.
    /// The quoted id (or the numeric id when there's no quote) becomes <see cref="DeviceProfile.Id"/>.
    /// </summary>
    public static IReadOnlyList<DeviceProfile> ParseDevices(string output)
    {
        var devices = new List<DeviceProfile>();

        string? id = null;
        string? name = null;
        string? oem = null;
        string? tag = null;

        void Flush()
        {
            if (id is not null)
            {
                devices.Add(new DeviceProfile(id, name ?? id, oem, tag));
            }

            id = null;
            name = null;
            oem = null;
            tag = null;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                Flush();
                id = ExtractId(line);
            }
            else if (line.StartsWith("Name:", StringComparison.Ordinal))
            {
                name = line["Name:".Length..].Trim();
            }
            else if (line.StartsWith("OEM", StringComparison.Ordinal) && line.IndexOf(':') is var oemColon and >= 0)
            {
                oem = line[(oemColon + 1)..].Trim();
            }
            else if (line.StartsWith("Tag", StringComparison.Ordinal) && line.IndexOf(':') is var tagColon and >= 0)
            {
                tag = line[(tagColon + 1)..].Trim();
            }
        }

        Flush();
        return devices;
    }

    // Prefers the quoted id (e.g. "pixel_6"); falls back to the numeric id before " or ".
    private static string ExtractId(string line)
    {
        var openQuote = line.IndexOf('"');
        if (openQuote >= 0)
        {
            var closeQuote = line.IndexOf('"', openQuote + 1);
            if (closeQuote > openQuote)
            {
                return line[(openQuote + 1)..closeQuote];
            }
        }

        var rest = line["id:".Length..].Trim();
        var orIndex = rest.IndexOf(" or ", StringComparison.Ordinal);
        return (orIndex >= 0 ? rest[..orIndex] : rest).Trim();
    }
}
