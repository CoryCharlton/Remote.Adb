using System.Diagnostics;

namespace Remote.Adb.Core.Common;

/// <summary>
/// Runs external command-line tools (adb, emulator, ssh, ...).
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> to completion and captures its output. When
    /// <paramref name="standardInput"/> is supplied it is written to the process's stdin (which is then
    /// closed) — needed for tools that prompt, such as <c>avdmanager create avd</c>. <paramref name="environment"/>
    /// overlays variables onto the child's environment (e.g. <c>JAVA_HOME</c> for the Java-based tools).
    /// </summary>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput = null, IReadOnlyDictionary<string, string>? environment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts <paramref name="fileName"/> without waiting for it to exit, for long-running
    /// processes such as a launched emulator. The caller owns the returned <see cref="Process"/>.
    /// </summary>
    Process Start(string fileName, IReadOnlyList<string> arguments);

    /// <summary>
    /// Starts <paramref name="fileName"/> as a supervised long-running process (e.g. an SSH reverse tunnel):
    /// its output is captured and its exit can be awaited via the returned <see cref="IProcessSession"/>.
    /// <paramref name="environment"/> overlays variables onto the child's environment.
    /// </summary>
    IProcessSession StartSession(string fileName, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null);
}
