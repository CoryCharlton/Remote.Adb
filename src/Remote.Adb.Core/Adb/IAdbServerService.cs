namespace Remote.Adb.Core.Adb;

/// <summary>
/// Manages the lifecycle of the local <c>adb</c> server — the server the SSH reverse tunnel forwards to.
/// </summary>
public interface IAdbServerService
{
    /// <summary>Raised after <see cref="RestartAsync"/> brings the server back up, so dependents (e.g. an open
    /// tunnel whose forward target just bounced) can react.</summary>
    event EventHandler ServerRestarted;

    /// <summary>
    /// Returns whether something is listening on the local adb server port. Uses a raw TCP probe so it does
    /// <b>not</b> auto-spawn a server the way an <c>adb</c> client command would.
    /// </summary>
    Task<bool> IsRunningAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>Stops the local adb server (<c>adb kill-server</c>).</summary>
    Task KillAsync(CancellationToken cancellationToken = default);

    /// <summary>Restarts the local adb server (kill then start) and raises <see cref="ServerRestarted"/>.</summary>
    Task RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>Ensures the local adb server is running (<c>adb start-server</c>).</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
}
