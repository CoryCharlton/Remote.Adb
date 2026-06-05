using System;
using System.Collections.Generic;
using System.Linq;

namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Rewrites an <see cref="IniDocument"/> applying a set of key changes, re-emitting every untouched line
/// (comments, blanks, unknown keys) verbatim. This unknown-key preservation is what lets the edit feature
/// change only the fields it understands without corrupting the rest of an AVD's <c>config.ini</c>.
/// Output newlines are normalized to <c>\n</c>.
/// </summary>
public static class AvdConfigWriter
{
    /// <summary>
    /// Returns the document's text with each key in <paramref name="changes"/> set to its new value — updated
    /// in place where the key already exists, appended (sorted) where it does not — and every key in
    /// <paramref name="removals"/> dropped. Removal lets the editor omit a cleared field rather than write it
    /// blank; a key in both <paramref name="changes"/> and <paramref name="removals"/> is set, not dropped.
    /// </summary>
    public static string Write(
        IniDocument document,
        IReadOnlyDictionary<string, string> changes,
        IReadOnlyCollection<string>? removals = null)
    {
        var pending = new Dictionary<string, string>(changes, StringComparer.Ordinal);
        var dropped = removals is null
            ? null
            : new HashSet<string>(removals, StringComparer.Ordinal);
        var output = new List<string>(document.Lines.Count + changes.Count);
        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in document.Lines)
        {
            if (line.Kind != IniLineKind.Pair || line.Key is null)
            {
                output.Add(line.Raw);
            }
            else if (pending.TryGetValue(line.Key, out var updated))
            {
                // Emit the new value at the first occurrence; drop any later duplicates of the same key so
                // the file can't keep a contradictory stale line for it.
                if (written.Add(line.Key))
                {
                    output.Add($"{line.Key}={updated}");
                }
            }
            else if (dropped is not null && dropped.Contains(line.Key))
            {
                // Omit the line entirely — the cleared key disappears from config.ini.
            }
            else
            {
                output.Add(line.Raw);
            }
        }

        // Keys not already present are appended in a stable (sorted) order so output is deterministic.
        foreach (var entry in pending.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!written.Contains(entry.Key))
            {
                output.Add($"{entry.Key}={entry.Value}");
            }
        }

        var text = string.Join('\n', output);
        return document.TrailingNewline ? text + "\n" : text;
    }
}
