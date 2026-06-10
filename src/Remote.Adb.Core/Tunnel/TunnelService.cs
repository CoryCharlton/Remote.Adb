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

    // How long the ssh process must stay up past launch before we treat the forward as established. With
    // ExitOnForwardFailure a losing bind exits well within this window. Internal so tests can shorten it.
    internal TimeSpan SettleWindow { get; set; } = TimeSpan.FromSeconds(1.5);

    /// <inheritdoc />
    public TunnelStatus Status => _status;

    /// <inheritdoc />
    public async Task ConnectAsync(string? host = null, CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_connectLock)
            {
                _connectCts = cts;
            }

            await ConnectCoreAsync(host, cts.Token);
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

    private async Task ConnectCoreAsync(string? host, CancellationToken cancellationToken)
    {
        await TearDownConnectionAsync();

        var resolvedHost = string.IsNullOrWhiteSpace(host) ? _settings.TunnelHost : host.Trim();
        if (string.IsNullOrWhiteSpace(resolvedHost))
        {
            SetStatus(TunnelState.Faulted, "No remote host configured.");
            return;
        }

        var ssh = _executableFinder.FindOnPath("ssh");
        if (ssh is null)
        {
            SetStatus(TunnelState.Faulted, "OpenSSH 'ssh' was not found on PATH.");
            return;
        }

        var remotePort = _settings.TunnelRemotePort;
        var localPort = _settings.TunnelLocalPort;
        var forwardSpec = $"{remotePort}:127.0.0.1:{localPort}";

        SetStatus(TunnelState.Connecting, null);

        try
        {
            await _adbServer.StartAsync(cancellationToken);

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
                        SetStatus(TunnelState.Faulted, $"ssh to {resolvedHost} did not respond within {AttemptTimeout.TotalSeconds:N0}s. Check that key-based ssh to the host works non-interactively (try it from a terminal).");
                        return;
                    }

                    continue;
                }

                if (!kill.Success)
                {
                    SetStatus(TunnelState.Faulted, $"Could not reach {resolvedHost} over SSH: {Detail(kill.StandardError, kill.ExitCode)}");
                    return;
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
                        SetStatus(TunnelState.Faulted, $"The reverse forward could not bind after {MaxRetries} attempts. {Detail(error, session.ExitCode)}");
                        return;
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
                return;
            }
        }
        catch (ProcessLaunchException exception)
        {
            SetStatus(TunnelState.Faulted, exception.Message);
        }
    }

    private static string Detail(string standardError, int? exitCode)
    {
        var trimmed = standardError.Trim();
        return trimmed.Length > 0 ? trimmed : $"ssh exited with code {exitCode}.";
    }

    // Faults the tunnel iff this connection is still the live one, then disposes it. Both monitors funnel through
    // here so they can't double-fault or double-dispose, and the loser of the race simply finds it isn't current.
    private async Task FaultIfCurrentAsync(TunnelConnection connection, Func<string> message, Action log)
    {
        var current = false;

        await _gate.WaitAsync();
        try
        {
            if (ReferenceEquals(_connection, connection))
            {
                current = true;
                _connection = null;
                log();
                SetStatus(TunnelState.Faulted, message());
            }
        }
        finally
        {
            _gate.Release();
        }

        if (current)
        {
            await connection.DisposeAsync();
        }
    }

    // While connected, the only thing the ssh-exit monitor can't see is the local adb server dying underneath a
    // still-bound forward (the remote running `adb kill-server` reaches it through the tunnel). Probe it; fault on
    // a sustained outage, but ride through a brief one (a deliberate `RestartAsync` bounces the server for ~1s).
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

                if (++failures < failuresBeforeFault)
                {
                    continue;
                }

                await FaultIfCurrentAsync(
                    connection,
                    () => "The local adb server stopped — it may have been killed through the tunnel. Reconnect to restart it.",
                    () => _logger.LogWarning("Local adb server is no longer listening on 127.0.0.1:{Port} — it may have been killed through the tunnel.", port));
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

        await FaultIfCurrentAsync(
            connection,
            () => $"The tunnel dropped. {Detail(connection.Session.StandardError, exitCode)}",
            () => _logger.LogWarning("Tunnel dropped (exit {ExitCode}): {Error}", exitCode, connection.Session.StandardError));
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
    // place a connection is retired from the owning side; a monitor retires its own via FaultIfCurrentAsync.
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
