namespace Remote.Adb.Core.Emulators;

/// <summary>The classification of a single line in an <see cref="IniDocument"/>.</summary>
public enum IniLineKind
{
    /// <summary>A <c>key=value</c> pair.</summary>
    Pair,

    /// <summary>A comment line (starts with <c>#</c>), or any non-blank line that isn't a pair.</summary>
    Comment,

    /// <summary>An empty / whitespace-only line.</summary>
    Blank,
}
