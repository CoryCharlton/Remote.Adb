namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Metadata read from an AVD's <c>config.ini</c>.
/// </summary>
/// <param name="AvdId">The <c>AvdId</c> — matches the id from <c>emulator -list-avds</c>.</param>
/// <param name="DisplayName">The <c>avd.ini.displayname</c>; falls back to <paramref name="AvdId"/>.</param>
/// <param name="Tag">The <c>tag.displaynames</c> device category, or <see langword="null"/>.</param>
public sealed record AvdMetadata(string AvdId, string DisplayName, string? Tag);
