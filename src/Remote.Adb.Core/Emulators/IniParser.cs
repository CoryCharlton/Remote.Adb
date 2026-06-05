using System.Collections.Generic;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Pure parsing of INI text into an ordered <see cref="IniDocument"/>. Order-preserving and
/// <c>\r\n</c>-safe; classifies each line as a <c>key=value</c> pair, a comment, or a blank. Kept free of
/// I/O so it is trivially unit-testable.
/// </summary>
public static class IniParser
{
    public static IniDocument Parse(string text)
    {
        var trailingNewline = text.EndsWith('\n');
        var rawLines = text.Split('\n');

        // Split('\n') yields a trailing empty element for the final newline; drop it and record the
        // trailing newline separately so a rewrite reproduces it without inventing a blank line.
        var count = trailingNewline && rawLines.Length > 0 ? rawLines.Length - 1 : rawLines.Length;

        var lines = new List<IniLine>(count);
        for (var i = 0; i < count; i++)
        {
            var raw = rawLines[i].TrimEnd('\r');
            var trimmed = raw.Trim();

            if (trimmed.Length == 0)
            {
                lines.Add(new IniLine(IniLineKind.Blank, null, null, raw));
                continue;
            }

            var separator = trimmed[0] == '#' ? -1 : raw.IndexOf('=');
            if (separator <= 0)
            {
                lines.Add(new IniLine(IniLineKind.Comment, null, null, raw));
                continue;
            }

            var key = raw[..separator].Trim();
            var value = raw[(separator + 1)..].Trim();
            lines.Add(new IniLine(IniLineKind.Pair, key, value, raw));
        }

        return new IniDocument(lines, trailingNewline);
    }
}
