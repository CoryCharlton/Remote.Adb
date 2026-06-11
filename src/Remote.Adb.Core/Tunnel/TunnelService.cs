using Microsoft.Extensions.Logging;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Settings;

namespace Remote.Adb.Core.Tunnel;

/// <inheritdoc cref="ITunnelService" />
public sealed class TunnelService : ITunnelService, IAsyncDisposable
{
    // IntelliJ's Android plugin respawns adb on Gradle sync and can win the bind race, so retry a few times.
    private const int MaxRetries = 3;

    // A GUI process has no terminal, so any password/passphrase/host-key prompt would hang ssh forever (and a
    // hung ssh that never exits looks "connected" once the settle window elapses). BatchMode fails fast instead;
    // ConnectTimeout bounds an unreachable host; accept-new trusts a new host key the way answering the
    // first-connect prompt would; GSSAPIAuthentication=no skips Kerberos, which can stall for a long time on a
    // corporate/AD network (a *.internal host) when no ticket is available — we authenticate by key anyway.
    private static readonly string[] SshConnectOptions =
    [
        "-o", "BatchMode=yes",
        "-o", "ConnectTimeout=10",
        "-o", "GSSAPIAuthentication=no",
        "-o", "StrictHostKeyChecking=accept-new",
    ];

    private readonly IAdbServerService _adbServer;
    private readonly object _connectLock = new();
    private readonly IExecutableFinder _executableFinder;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<TunnelService> _logger;
    private readonly IProcessRunner _processRunner;
    private readonly ISettingsService _settings;
    private TunnelConnection? _connection;
    private CancellationTokenSource? _connectCts;
    private CancellationTokenSource? _reconnectCts;
    private TunnelStatus _status = new(TunnelState.Disconnected, null);

    public TunnelService(IProcessRunner processRunner, IAdbServerService adbServer, ISettingsService settings, IExecutableFinder executableFinder, ILogger<TunnelService> logger)
    {
        _processRunner = processRunner;
        _adbServer = adbServer;
        _settings = settings;
        _executableFinder = executableFinder;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<TunnelStatus>? StatusChanged;

    // Upper bound on a single ssh round-trip (the remote-adb kill). ConnectTimeout only bounds the TCP connect;
    // this catches a stall *after* connecting (e.g. slow auth) so the retry loop advances instead of hanging.
    // Internal so tests can shorten it.
    internal TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(15);

    // While connected, how often to probe that the local adb server is still listening — the remote can kill it
    // through the forward (`adb kill-server`), which leaves the ssh forward bound but pointing at a dead port.
    // Internal so tests can shorten it.
    internal TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(5);

    // When a connected tunnel drops on its own, auto-reconnect this many times (with a growing backoff) before
    // giving up and faulting. Internal so tests can shorten/shrink them.
    internal int MaxReconnectAttempts { get; set; } = 5;

    internal TimeSpan ReconnectBackoff { get; set; } = TimeSpan.FromSeconds(2);

    // How long the ssh process must stay up past launch before we treat the forward as established. With
    // ExitOnForwardFailure a losing bind exits well within this window. Internal so tests can shorten it.
    internal TimeSpan SettleWindow { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <inheritdoc />
    public TunnelStatus Status => _status;

    /// <inheritdoc />
    public async Task ConnectAsync(string? host = null, CancellationToken cancellationToken = default)
    {
        CancelReconnect();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_connectLock)
            {
                _connectCts = cts;
            }

            SetStatus(TunnelState.Connecting, null);
            var error = await TryConnectAsync(host, cts.Token);
            if (error is not null)
            {
                SetStatus(TunnelState.Faulted, error);
            }
        }
        catch (OperationCanceledException)
        {
            await TearDownConnectionAsync();
            SetStatus(TunnelState.Disconnected, null);
        }
        finally
        {
            lock (_connectLock)
            {
                _connectCts = null;
                cts.Dispose();
            }

            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        CancelReconnect();
        CancelConnectInFlight();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await TearDownConnectionAsync();
            SetStatus(TunnelState.Disconnected, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        CancelReconnect();
        CancelConnectInFlight();

        await _gate.WaitAsync();
        try
        {
            await TearDownConnectionAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Cancels an in-flight connect (if any). The lock pairs with ConnectAsync's finally so cancel and the
    // disposal of the same linked CTS are mutually exclusive — no Cancel()-on-disposed-CTS race.
    private void CancelConnectInFlight()
    {
        lock (_connectLock)
        {
            _connectCts?.Cancel();
        }
    }

    // Cancels an in-flight reconnect supervisor (if any), so a user Connect/Disconnect supersedes it.
    private void CancelReconnect()
    {
        lock (_connectLock)
        {
            _reconnectCts?.Cancel();
        }
    }

    private static string Detail(string standardError, int? exitCode)
    {
        var trimmed = standardError.Trim();
        return trimmed.Length > 0 ? trimmed : $"ssh exited with code {exitCode}.";
    }

    private async Task MonitorServerHealthAsync(TunnelConnection connection, int port)
    {
        const int failuresBeforeFault = 2;
        var failures = 0;
        var token = connection.Cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(HealthCheckInterval, token);

                if (await _adbServer.IsRunningAsync(port, token))
                {
                    failures = 0;
                    continue;
                }

                // Ride through a brief outage (a deliberate `RestartAsync` bounces the server for ~1s); only act
                // on a sustained one (the remote ran `adb kill-server` through the forward).
                if (++failures < failuresBeforeFault)
                {
                    continue;
                }

                await OnConnectionLostAsync(
                    connection,
                    "The local adb server stopped.",
                    () => _logger.LogWarning("Local adb server is no longer listening on 127.0.0.1:{Port}.", port));
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task MonitorSshExitAsync(TunnelConnection connection)
    {
        int exitCode;
        try
        {
            exitCode = await connection.ExitTask;
        }
        catch
        {
            exitCode = -1;
        }

        await OnConnectionLostAsync(
            connection,
            $"The tunnel dropped ({Detail(connection.Session.StandardError, exitCode)})",
            () => _logger.LogWarning("Tunnel dropped (exit {ExitCode}): {Error}", exitCode, connection.Session.StandardError));
    }

    // A monitor saw the live connection drop on its own (ssh exited, or the local adb server died). Retire it and
    // kick off the reconnect supervisor. Iff this connection is still the current one — the loser of a race (or a
    // user Disconnect, which nulls _connection first under the gate) finds it isn't current and does nothing.
    private async Task OnConnectionLostAsync(TunnelConnection connection, string reason, Action log)
    {
        var lost = false;
        CancellationTokenSource? reconnectCts = null;

        await _gate.WaitAsync();
        try
        {
            if (ReferenceEquals(_connection, connection))
            {
                lost = true;
                _connection = null;
                log();

                reconnectCts = new CancellationTokenSource();
                lock (_connectLock)
                {
                    _reconnectCts = reconnectCts;
                }

                SetStatus(TunnelState.Reconnecting, $"{reason} Reconnecting…");
            }
        }
        finally
        {
            _gate.Release();
        }

        if (lost)
        {
            await connection.DisposeAsync();
            _ = ReconnectAsync(reconnectCts!);
        }
    }

    // Re-establishes a dropped tunnel with a growing backoff, up to MaxReconnectAttempts, before faulting. Runs
    // fire-and-forget; cancelled (and superseded) by a user Connect/Disconnect/Dispose via CancelReconnect.
    private async Task ReconnectAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        string? error = null;

        try
        {
            for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
            {
                await Task.Delay(ReconnectBackoff * attempt, token);

                await _gate.WaitAsync(token);
                try
                {
                    SetStatus(TunnelState.Reconnecting, $"Reconnecting… (attempt {attempt}/{MaxReconnectAttempts})");
                    error = await TryConnectAsync(null, token);
                }
                finally
                {
                    _gate.Release();
                }

                if (error is null)
                {
                    return;
                }

                _logger.LogWarning("Tunnel reconnect attempt {Attempt}/{Max} failed: {Error}", attempt, MaxReconnectAttempts, error);
            }

            await _gate.WaitAsync(token);
            try
            {
                SetStatus(TunnelState.Faulted, $"Lost the tunnel and couldn't reconnect after {MaxReconnectAttempts} attempts. {error}");
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_connectLock)
            {
                if (ReferenceEquals(_reconnectCts, cts))
                {
                    _reconnectCts = null;
                }
            }

            cts.Dispose();
        }
    }

    private void SetStatus(TunnelState state, string? message)
    {
        _status = new TunnelStatus(state, message);

        if (state == TunnelState.Faulted)
        {
            _logger.LogWarning("Tunnel faulted: {Message}", message);
        }
        else
        {
            _logger.LogInformation("Tunnel {State}{Message}", state, message is null ? string.Empty : $": {message}");
        }

        StatusChanged?.Invoke(this, _status);
    }

    // Tears down the live connection, if any. Callers hold _gate (Connect/Disconnect/Dispose), so this is the one
    // place a connection is retired from the owning side; a monitor retires its own via OnConnectionLostAsync.
    private async Task TearDownConnectionAsync()
    {
        var existing = _connection;
        if (existing is null)
        {
            return;
        }

        _connection = null;
        await existing.DisposeAsync();
    }

    // Runs the full connect sequence (shared by a manual ConnectAsync and the reconnect supervisor). On success,
    // stores the connection, starts the monitors, sets Connected, and returns null. On failure it returns the
    // error message *without* faulting, so the caller decides (a manual connect faults; a reconnect retries).
    // Throws OperationCanceledException if cancelled. Callers hold _gate.
    private async Task<string?> TryConnectAsync(string? host, CancellationToken cancellationToken)
    {
        await TearDownConnectionAsync();

        var resolvedHost = string.IsNullOrWhiteSpace(host) ? _settings.TunnelHost : host.Trim();
        if (string.IsNullOrWhiteSpace(resolvedHost))
        {
            return "No remote host configured.";
        }

        var ssh = _executableFinder.FindOnPath("ssh");
        if (ssh is null)
        {
            return "OpenSSH 'ssh' was not found on PATH.";
        }

        var remotePort = _settings.TunnelRemotePort;
        var localPort = _settings.TunnelLocalPort;
        var forwardSpec = $"{remotePort}:127.0.0.1:{localPort}";

        try
        {
            await _adbServer.StartAsync(cancellationToken);

            // The forward target is the local adb server; if it isn't listening (e.g. start-server couldn't bring
            // it back), the tunnel would be useless — fail now rather than declare a dead-target "connection".
            // This also bounds reconnect: a drop caused by a server that stays down can't loop forever.
            if (!await _adbServer.IsRunningAsync(localPort, cancellationToken))
            {
                return $"The local adb server is not listening on 127.0.0.1:{localPort}.";
            }

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                _logger.LogInformation("Tunnel attempt {Attempt}/{Max}: killing remote adb on {Host}, then binding {Forward}", attempt, MaxRetries, resolvedHost, forwardSpec);

                // Kill any adb the remote IntelliJ respawned, which would already hold the reverse-forward port.
                // pkill -x (exact name) is signal-only — never `adb kill-server`, which can hang on the forward.
                // Bound it: a stalled ssh (slow auth) would otherwise hang this attempt forever — kill it and retry.
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(AttemptTimeout);

                ProcessResult kill;
                try
                {
                    kill = await _processRunner.RunAsync(ssh, [.. SshConnectOptions, resolvedHost, "pkill -x adb >/dev/null 2>&1; true"], cancellationToken: attemptCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Tunnel attempt {Attempt}/{Max}: ssh to {Host} did not respond within {Timeout:N0}s.", attempt, MaxRetries, resolvedHost, AttemptTimeout.TotalSeconds);

                    if (attempt == MaxRetries)
                    {
                        return $"ssh to {resolvedHost} did not respond within {AttemptTimeout.TotalSeconds:N0}s. Check that key-based ssh to the host works non-interactively (try it from a terminal).";
                    }

                    continue;
                }

                if (!kill.Success)
                {
                    return $"Could not reach {resolvedHost} over SSH: {Detail(kill.StandardError, kill.ExitCode)}";
                }

                var session = _processRunner.StartSession(ssh, [.. SshConnectOptions, "-o", "ExitOnForwardFailure=yes", "-N", "-R", forwardSpec, resolvedHost]);
                var exitTask = session.WaitForExitAsync();

                var settled = await Task.WhenAny(exitTask, Task.Delay(SettleWindow, cancellationToken));
                if (settled == exitTask)
                {
                    await exitTask;
                    var error = session.StandardError;
                    await session.DisposeAsync();
                    _logger.LogWarning("Tunnel bind attempt {Attempt}/{Max} exited early: {Error}", attempt, MaxRetries, error);

                    if (attempt == MaxRetries)
                    {
                        return $"The reverse forward could not bind after {MaxRetries} attempts. {Detail(error, session.ExitCode)}";
                    }

                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    session.Kill();
                    await session.DisposeAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var connection = new TunnelConnection(session, exitTask);
                _connection = connection;
                SetStatus(TunnelState.Connected, $"Forwarding {resolvedHost}:{remotePort} → 127.0.0.1:{localPort}");
                _ = MonitorSshExitAsync(connection);
                _ = MonitorServerHealthAsync(connection, localPort);
                return null;
            }

            return "The tunnel could not be established.";
        }
        catch (ProcessLaunchException exception)
        {
            return exception.Message;
        }
    }

    // One live tunnel: the ssh session, the task that completes when it exits, and a CTS bounding the connected
    // phase (cancelled on teardown to stop the health monitor). Disposing kills the session; idempotent because
    // both the owning teardown and a faulting monitor can reach it, though the coordination ensures only one does.
    private sealed class TunnelConnection
    {
        private bool _disposed;

        public TunnelConnection(IProcessSession session, Task<int> exitTask)
        {
            Session = session;
            ExitTask = exitTask;
        }

        public CancellationTokenSource Cts { get; } = new();

        public Task<int> ExitTask { get; }

        public IProcessSession Session { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Cts.Cancel();
            Cts.Dispose();
            Session.Kill();
            await Session.DisposeAsync();
        }
    }
}
