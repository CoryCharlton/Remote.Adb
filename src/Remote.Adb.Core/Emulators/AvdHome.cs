namespace Remote.Adb.Core.Emulators;

/// <summary>
/// Resolves the Android AVD home — where the <c>*.avd</c> folders and their sibling <c>&lt;AvdId&gt;.ini</c>
/// files live: the Settings override, else <c>ANDROID_AVD_HOME</c>, else <c>$ANDROID_SDK_HOME/.android/avd</c>,
/// else <c>~/.android/avd</c>.
/// </summary>
public static class AvdHome
{
    /// <summary>
    /// The first existing AVD home from the resolution order (<paramref name="overridePath"/> wins), or
    /// <see langword="null"/>.
    /// </summary>
    public static string? Resolve(string? overridePath = null)
    {
        var candidates = new[]
        {
            overridePath,
            Environment.GetEnvironmentVariable("ANDROID_AVD_HOME"),
            DotAndroidAvd(Environment.GetEnvironmentVariable("ANDROID_SDK_HOME")),
            DotAndroidAvd(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        };

        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate));
    }

    private static string? DotAndroidAvd(string? root) =>
        string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, ".android", "avd");
}
