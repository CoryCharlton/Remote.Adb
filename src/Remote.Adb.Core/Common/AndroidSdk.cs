using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Settings;

namespace Remote.Adb.Core.Common;

/// <inheritdoc />
public sealed class AndroidSdk : IAndroidSdk
{
    private readonly ISettingsService _settings;

    public AndroidSdk(ISettingsService settings, ILogger<AndroidSdk> logger)
    {
        _settings = settings;

        // Resolution happens live on each property access (so a Settings override applies without a restart);
        // resolve once here only to log the startup snapshot for diagnostics.
        var (root, source) = ResolveSdkRoot();
        if (root is null)
        {
            logger.LogWarning(
                "Android SDK root not found (set ANDROID_HOME or the Settings override); falling back to PATH for adb/emulator/avdmanager.");
        }
        else
        {
            logger.LogDebug("Using Android SDK at {SdkRoot} (source {Source})", root, source);
        }
    }

    /// <inheritdoc />
    public string AdbPath => ResolveTool("platform-tools", Executable("adb"));

    /// <inheritdoc />
    public string AvdManagerPath => ResolveCmdlineTool(Script("avdmanager"));

    /// <inheritdoc />
    public string EmulatorPath => ResolveTool("emulator", Executable("emulator"));

    /// <inheritdoc />
    public string? JavaHome
    {
        get
        {
            // No install-path probing: the Java tools find a JDK themselves via JAVA_HOME or PATH, so only
            // surface an explicit value (the override, else JAVA_HOME) for callers to pass through.
            var value = _settings.JavaHome;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var env = Environment.GetEnvironmentVariable("JAVA_HOME");
            return string.IsNullOrWhiteSpace(env) ? null : env;
        }
    }

    /// <inheritdoc />
    public string SdkManagerPath => ResolveCmdlineTool(Script("sdkmanager"));

    /// <inheritdoc />
    public string? SdkRoot => ResolveSdkRoot().Root;

    /// <inheritdoc />
    public SdkRootSource SdkRootSource => ResolveSdkRoot().Source;

    private static string DefaultSdkRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Android", "Sdk");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Android", "sdk");
        }

        return Path.Combine(home, "Android", "Sdk");
    }

    private static string Executable(string name) => OperatingSystem.IsWindows() ? $"{name}.exe" : name;

    // avdmanager/sdkmanager live in cmdline-tools/<version>/bin (newest preferred), with a legacy copy in
    // tools/bin on older SDKs. Pick the first that actually exists, else the canonical path so callers still
    // get a usable value.
    private string ResolveCmdlineTool(string executable)
    {
        var sdkRoot = SdkRoot;
        if (sdkRoot is null)
        {
            return executable;
        }

        var candidates = new List<string>
        {
            Path.Combine(sdkRoot, "cmdline-tools", "latest", "bin", executable),
        };

        var cmdlineTools = Path.Combine(sdkRoot, "cmdline-tools");
        if (Directory.Exists(cmdlineTools))
        {
            try
            {
                foreach (var versionDirectory in Directory.EnumerateDirectories(cmdlineTools).OrderDescending())
                {
                    candidates.Add(Path.Combine(versionDirectory, "bin", executable));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Can't enumerate the cmdline-tools versions — fall through to the canonical/legacy candidates.
            }
        }

        candidates.Add(Path.Combine(sdkRoot, "tools", "bin", executable));

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    // Override → ANDROID_HOME → ANDROID_SDK_ROOT (deprecated) → platform default, first that exists. The source
    // is reported so the UI can warn when the path is only the default guess (or missing entirely).
    private (string? Root, SdkRootSource Source) ResolveSdkRoot()
    {
        var overrideRoot = _settings.SdkRoot;
        if (!string.IsNullOrWhiteSpace(overrideRoot) && Directory.Exists(overrideRoot))
        {
            return (overrideRoot, SdkRootSource.Override);
        }

        foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            {
                return (value, SdkRootSource.EnvironmentVariable);
            }
        }

        var fallback = DefaultSdkRoot();
        return Directory.Exists(fallback)
            ? (fallback, SdkRootSource.DefaultFallback)
            : (null, SdkRootSource.NotFound);
    }

    private string ResolveTool(string relativeDirectory, string executable)
    {
        var sdkRoot = SdkRoot;

        // No SDK root: rely on the tool being discoverable on PATH.
        return sdkRoot is null ? executable : Path.Combine(sdkRoot, relativeDirectory, executable);
    }

    private static string Script(string name) => OperatingSystem.IsWindows() ? $"{name}.bat" : name;
}
