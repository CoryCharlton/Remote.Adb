namespace Remote.Adb.Core.Emulators;

/// <summary>
/// An ordered, parsed INI file. Preserves every line (comments, blanks, unknown keys) so an edit can
/// rewrite the keys it cares about and re-emit the rest unchanged. Built by <see cref="IniParser"/>.
/// </summary>
public sealed class IniDocument
{
    private readonly IReadOnlyList<IniLine> _lines;

    public IniDocument(IReadOnlyList<IniLine> lines, bool trailingNewline)
    {
        _lines = lines;
        TrailingNewline = trailingNewline;
    }

    /// <summary>The lines in source order.</summary>
    public IReadOnlyList<IniLine> Lines => _lines;

    /// <summary>Whether the source text ended with a trailing newline (so a rewrite can reproduce it).</summary>
    public bool TrailingNewline { get; }

    /// <summary>The value of the first <see cref="IniLineKind.Pair"/> with this key, or <see langword="null"/>.</summary>
    public string? Get(string key)
    {
        foreach (var line in _lines)
        {
            if (line.Kind == IniLineKind.Pair && string.Equals(line.Key, key, StringComparison.Ordinal))
            {
                return line.Value;
            }
        }

        return null;
    }
}
