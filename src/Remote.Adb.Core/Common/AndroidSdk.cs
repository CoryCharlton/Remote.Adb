using Microsoft.Extensions.Logging;

namespace Remote.Adb.Core.Common;

/// <inheritdoc />
public sealed class AndroidSdk : IAndroidSdk
{
    public AndroidSdk(ILogger<AndroidSdk> logger)
    {
        SdkRoot = ResolveSdkRoot();

        if (SdkRoot is null)
        {
            logger.LogWarning(
                "Android SDK root not found (set ANDROID_HOME); falling back to PATH for adb/emulator/avdmanager.");
        }
        else
        {
            logger.LogDebug("Using Android SDK at {SdkRoot}", SdkRoot);
        }

        AdbPath = ResolveTool("platform-tools", Executable("adb"));
        EmulatorPath = ResolveTool("emulator", Executable("emulator"));
        AvdManagerPath = ResolveCmdlineTool(Script("avdmanager"));
        SdkManagerPath = ResolveCmdlineTool(Script("sdkmanager"));
    }

    /// <inheritdoc />
    public string AdbPath { get; }

    /// <inheritdoc />
    public string AvdManagerPath { get; }

    /// <inheritdoc />
    public string EmulatorPath { get; }

    /// <inheritdoc />
    public string SdkManagerPath { get; }

    /// <inheritdoc />
    public string? SdkRoot { get; }

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
        if (SdkRoot is null)
        {
            return executable;
        }

        var candidates = new List<string>
        {
            Path.Combine(SdkRoot, "cmdline-tools", "latest", "bin", executable),
        };

        var cmdlineTools = Path.Combine(SdkRoot, "cmdline-tools");
        if (Directory.Exists(cmdlineTools))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(cmdlineTools).OrderDescending())
            {
                candidates.Add(Path.Combine(versionDirectory, "bin", executable));
            }
        }

        candidates.Add(Path.Combine(SdkRoot, "tools", "bin", executable));

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string? ResolveSdkRoot()
    {
        var candidates = new[]
        {
            // ANDROID_HOME is the current variable; ANDROID_SDK_ROOT is deprecated but still honored as a
            // fallback. The platform default (Android Studio's install location) is the last resort.
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            DefaultSdkRoot(),
        };

        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate));
    }

    private string ResolveTool(string relativeDirectory, string executable)
    {
        if (SdkRoot is null)
        {
            // No SDK root: rely on the tool being discoverable on PATH.
            return executable;
        }

        return Path.Combine(SdkRoot, relativeDirectory, executable);
    }

    private static string Script(string name) => OperatingSystem.IsWindows() ? $"{name}.bat" : name;
}
