namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Pure parsing of <c>emulator</c> / emulator-console command output. Kept free of I/O so it is
/// trivially unit-testable.
/// </summary>
public static class EmulatorOutputParser
{
    /// <summary>
    /// Parses the output of <c>emulator -list-avds</c> into AVD names (one per line).
    /// </summary>
    public static IReadOnlyList<string> ParseAvdList(string output)
    {
        var names = new List<string>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length != 0)
            {
                names.Add(line);
            }
        }

        return names;
    }

    /// <summary>
    /// Parses the reply of <c>adb -s &lt;serial&gt; emu avd name</c>, which prints the AVD name
    /// followed by an <c>OK</c> status line. Returns the name, or <see langword="null"/> if absent.
    /// </summary>
    public static string? ParseAvdName(string output)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0
                || line.Equals("OK", StringComparison.Ordinal)
                || line.Equals("KO", StringComparison.Ordinal))
            {
                continue;
            }

            return line;
        }

        return null;
    }
}
