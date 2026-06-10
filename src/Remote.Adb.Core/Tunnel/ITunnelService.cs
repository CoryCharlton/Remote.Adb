namespace Remote.Adb.Core.Tunnel;

/// <summary>
/// Opens and supervises the SSH reverse tunnel that bridges the local adb server to a remote dev host
/// (<c>ssh -o ExitOnForwardFailure=yes -N -R &lt;remote&gt;:127.0.0.1:&lt;local&gt; host</c>), handling the
/// kill-then-bind-then-retry dance the IntelliJ adb-respawn race requires.
/// </summary>
public interface ITunnelService
{
    /// <summary>The current tunnel status.</summary>
    TunnelStatus Status { get; }

    /// <summary>Raised whenever <see cref="Status"/> changes. May be raised from a background thread.</summary>
    event EventHandler<TunnelStatus> StatusChanged;

    /// <summary>
    /// Opens the tunnel. <paramref name="host"/> overrides the configured host (<see cref="Settings.ISettingsService.TunnelHost"/>)
    /// when supplied. Any existing tunnel is torn down first.
    /// </summary>
    Task ConnectAsync(string? host = null, CancellationToken cancellationToken = default);

    /// <summary>Closes the tunnel, if open.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
