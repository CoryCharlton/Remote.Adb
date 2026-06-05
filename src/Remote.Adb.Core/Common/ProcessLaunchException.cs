namespace Remote.Adb.Core.Common;

/// <summary>
/// Thrown when an external tool cannot be launched — typically because the executable is not
/// installed or not on <c>PATH</c> (e.g. the Android SDK is missing).
/// </summary>
public sealed class ProcessLaunchException : Exception
{
    public ProcessLaunchException(string fileName, Exception innerException)
        : base($"Could not start '{fileName}'. Ensure it is installed and on PATH (set ANDROID_HOME for the Android SDK tools).", innerException)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}
