namespace Remote.Adb.Core.Emulators;

/// <summary>
/// One line of an INI file. <paramref name="Raw"/> preserves the original text so unrelated lines
/// round-trip verbatim; <paramref name="Key"/>/<paramref name="Value"/> are set only for
/// <see cref="IniLineKind.Pair"/> lines.
/// </summary>
public sealed record IniLine(IniLineKind Kind, string? Key, string? Value, string Raw);
