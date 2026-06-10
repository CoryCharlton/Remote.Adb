namespace Remote.Adb.Core.Common;

/// <summary>
/// A handle to a supervised long-running process (e.g. an SSH reverse tunnel): its output is captured and
/// its exit can be awaited, unlike the fire-and-forget <see cref="IProcessRunner.Start"/>. Disposing kills
/// the process tree if it is still running.
/// </summary>
public interface IProcessSession : IAsyncDisposable
{
    /// <summary>Whether the process has exited.</summary>
    bool HasExited { get; }

    /// <summary>The exit code once the process has exited, or <see langword="null"/> while it is still running.</summary>
    int? ExitCode { get; }

    /// <summary>
    /// The captured standard error. Fully populated once <see cref="WaitForExitAsync"/> has returned (so a
    /// caller that observes the exit can read the diagnostics that explain it).
    /// </summary>
    string StandardError { get; }

    /// <summary>Kills the process tree if it is still running. Safe to call more than once.</summary>
    void Kill();

    /// <summary>
    /// Waits for the process to exit and returns its exit code. <see cref="StandardError"/> is guaranteed to be
    /// fully captured by the time the returned task completes.
    /// </summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);
}
