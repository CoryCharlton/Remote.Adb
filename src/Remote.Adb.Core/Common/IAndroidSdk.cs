namespace Remote.Adb.Core.Common;

/// <summary>
/// Resolves the locations of the Android SDK command-line tools.
/// </summary>
public interface IAndroidSdk
{
    /// <summary>Path to the <c>adb</c> executable.</summary>
    string AdbPath { get; }

    /// <summary>Path to the <c>avdmanager</c> executable.</summary>
    string AvdManagerPath { get; }

    /// <summary>Path to the <c>emulator</c> executable.</summary>
    string EmulatorPath { get; }

    /// <summary>
    /// The Java home (JDK) to launch the Java-based tools (<c>avdmanager</c>/<c>sdkmanager</c>) with — the
    /// Settings override, else <c>JAVA_HOME</c> — or <see langword="null"/> to let the tools find <c>java</c> on
    /// <c>PATH</c> themselves.
    /// </summary>
    string? JavaHome { get; }

    /// <summary>Path to the <c>sdkmanager</c> executable.</summary>
    string SdkManagerPath { get; }

    /// <summary>
    /// The resolved SDK root, or <see langword="null"/> if it could not be located (in which case
    /// the tool paths fall back to bare executable names resolved via <c>PATH</c>).
    /// </summary>
    string? SdkRoot { get; }

    /// <summary>Where <see cref="SdkRoot"/> was resolved from, for surfacing or warning in the UI.</summary>
    SdkRootSource SdkRootSource { get; }
}
