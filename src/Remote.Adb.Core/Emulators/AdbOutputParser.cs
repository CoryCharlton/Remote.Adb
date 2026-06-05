namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Pure parsing of <c>adb</c> command output. Kept free of I/O so it is trivially unit-testable.
/// </summary>
public static class AdbOutputParser
{
    private static readonly char[] WhitespaceSeparators = [' ', '\t'];

    /// <summary>
    /// Parses the output of <c>adb devices</c>, returning the serials of devices in the
    /// <c>device</c> (online) state. The header line and offline/unauthorized entries are skipped.
    /// </summary>
    public static IReadOnlyList<string> ParseDevices(string output)
    {
        var serials = new List<string>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2 && parts[1].Equals("device", StringComparison.Ordinal))
            {
                serials.Add(parts[0]);
            }
        }

        return serials;
    }
}
