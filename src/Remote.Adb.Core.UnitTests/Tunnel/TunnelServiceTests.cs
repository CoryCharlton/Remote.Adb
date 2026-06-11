using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.Tunnel;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Tunnel;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class TunnelServiceTests
{
    private const string SshPath = "/usr/bin/ssh";

    private TunnelService? _service;

    [TearDown]
    public async Task TearDown()
    {
        if (_service is not null)
        {
            await _service.DisposeAsync();
            _service = null;
        }
    }

    private TunnelService CreateService(
        Mock<IProcessRunner> processRunner,
        out Mock<IAdbServerService> adbServer,
        string? configuredHost = "devhost",
        string? ssh = SshPath)
    {
        adbServer = new Mock<IAdbServerService>();
        adbServer.Setup(a => a.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        adbServer.Setup(a => a.IsRunningAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.TunnelHost).Returns(configuredHost);
        settings.SetupGet(s => s.TunnelRemotePort).Returns(5037);
        settings.SetupGet(s => s.TunnelLocalPort).Returns(5037);

        var finder = new Mock<IExecutableFinder>();
        finder.Setup(f => f.FindOnPath("ssh")).Returns(ssh);

        _service = new TunnelService(processRunner.Object, adbServer.Object, settings.Object, finder.Object, new LoggerFake<TunnelService>())
        {
            SettleWindow = TimeSpan.FromMilliseconds(50),
            HealthCheckInterval = TimeSpan.FromMilliseconds(20),
            ReconnectBackoff = TimeSpan.FromMilliseconds(10),
            MaxReconnectAttempts = 3,
        };

        return _service;
    }

    private static void SetupRemoteKill(Mock<IProcessRunner> processRunner, int exitCode = 0, string standardError = "")
    {
        processRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(exitCode, string.Empty, standardError));
    }

    private static void SetupStartSession(Mock<IProcessRunner> processRunner, Func<IProcessSession> factory)
    {
        processRunner
            .Setup(r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Returns(factory);
    }

    private static async Task<TunnelStatus> WaitForStateAsync(ITunnelService service, TunnelState state)
    {
        var tcs = new TaskCompletionSource<TunnelStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, TunnelStatus status)
        {
            if (status.State == state)
            {
                tcs.TrySetResult(status);
            }
        }

        service.StatusChanged += Handler;
        try
        {
            if (service.Status.State == state)
            {
                return service.Status;
            }

            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            service.StatusChanged -= Handler;
        }
    }

    public class When_ConnectAsync_Is_Called : TunnelServiceTests
    {
        [Test]
        public async Task It_faults_when_no_host_is_configured()
        {
            var processRunner = new Mock<IProcessRunner>();
            var service = CreateService(processRunner, out _, configuredHost: null);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Faulted));
            Assert.That(service.Status.Message, Does.Contain("No remote host"));
        }

        [Test]
        public async Task It_faults_when_ssh_is_not_on_path()
        {
            var processRunner = new Mock<IProcessRunner>();
            var service = CreateService(processRunner, out _, ssh: null);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Faulted));
            Assert.That(service.Status.Message, Does.Contain("ssh"));
        }

        [Test]
        public async Task It_connects_when_the_forward_stays_up()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            SetupStartSession(processRunner, () => new FakeProcessSession());

            var service = CreateService(processRunner, out var adbServer);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));
            Assert.That(service.Status.Message, Does.Contain("127.0.0.1:5037"));
            adbServer.Verify(a => a.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            processRunner.Verify(
                r => r.StartSession(SshPath, It.Is<IReadOnlyList<string>>(a =>
                    a.Contains("-N") && a.Contains("-R") && a.Contains("5037:127.0.0.1:5037")
                    && a.Contains("ExitOnForwardFailure=yes") && a.Contains("devhost")), It.IsAny<IReadOnlyDictionary<string, string>?>()),
                Times.Once);
        }

        [Test]
        public async Task It_kills_the_remote_adb_before_binding()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            SetupStartSession(processRunner, () => new FakeProcessSession());

            var service = CreateService(processRunner, out _);

            await service.ConnectAsync();

            processRunner.Verify(
                r => r.RunAsync(SshPath, It.Is<IReadOnlyList<string>>(a => a.Contains("devhost") && a.Any(part => part.Contains("pkill -x adb"))), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task It_retries_the_bind_race_then_connects()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            processRunner
                .SetupSequence(r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()))
                .Returns(new FakeProcessSession(immediateExitCode: 255, standardError: "remote port forwarding failed"))
                .Returns(new FakeProcessSession());

            var service = CreateService(processRunner, out _);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));
            processRunner.Verify(
                r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()),
                Times.Exactly(2));
        }

        [Test]
        public async Task It_faults_when_every_bind_attempt_fails()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            SetupStartSession(processRunner, () => new FakeProcessSession(immediateExitCode: 255, standardError: "remote port forwarding failed"));

            var service = CreateService(processRunner, out _);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Faulted));
            processRunner.Verify(
                r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()),
                Times.Exactly(3));
        }

        [Test]
        public async Task It_faults_when_the_remote_is_unreachable()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner, exitCode: 255, standardError: "Could not resolve hostname");

            var service = CreateService(processRunner, out _);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Faulted));
            processRunner.Verify(
                r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()),
                Times.Never);
        }

        [Test]
        public async Task It_times_out_a_stalled_ssh_and_faults_after_exhausting_attempts()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .Returns(async (string _, IReadOnlyList<string> _, string? _, IReadOnlyDictionary<string, string>? _, CancellationToken ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return new ProcessResult(0, string.Empty, string.Empty);
                });

            var service = CreateService(processRunner, out _);
            service.AttemptTimeout = TimeSpan.FromMilliseconds(40);

            await service.ConnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Faulted));
            Assert.That(service.Status.Message, Does.Contain("did not respond"));
            processRunner.Verify(
                r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Test]
        public async Task It_faults_after_reconnects_fail_when_the_local_adb_server_stays_down()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            var session = new FakeProcessSession();
            SetupStartSession(processRunner, () => session);

            var service = CreateService(processRunner, out var adbServer);
            await service.ConnectAsync();
            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));

            // Server dies and stays down: the health monitor drops the tunnel, and every reconnect fails its
            // server-liveness check, so after the bounded attempts it faults rather than looping forever.
            adbServer.Setup(a => a.IsRunningAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var status = await WaitForStateAsync(service, TunnelState.Faulted);

            Assert.That(status.Message, Does.Contain("reconnect"));
            Assert.That(session.KillCount, Is.GreaterThanOrEqualTo(1));
        }
    }

    public class When_DisconnectAsync_Is_Called : TunnelServiceTests
    {
        [Test]
        public async Task It_kills_the_session_and_reports_disconnected()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            var session = new FakeProcessSession();
            SetupStartSession(processRunner, () => session);

            var service = CreateService(processRunner, out _);

            await service.ConnectAsync();
            await service.DisconnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Disconnected));
            Assert.That(session.KillCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task It_survives_overlapping_connect_and_disconnect_without_throwing()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            SetupStartSession(processRunner, () => new FakeProcessSession());

            var service = CreateService(processRunner, out _);

            // Overlap connect with a disconnect that cancels the in-flight connect; the connect CTS is shared, so
            // a Cancel()-on-disposed-CTS race would surface here as an exception out of one of the awaited tasks.
            for (var i = 0; i < 25; i++)
            {
                await Task.WhenAll(service.ConnectAsync(), service.DisconnectAsync());
            }

            await service.DisconnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Disconnected));
        }
    }

    public class When_The_Tunnel_Drops : TunnelServiceTests
    {
        [Test]
        public async Task It_auto_reconnects_after_a_connected_session_exits()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);

            var first = new FakeProcessSession();
            var second = new FakeProcessSession();
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = 0;
            SetupStartSession(processRunner, () =>
            {
                if (++starts == 1)
                {
                    return first;
                }

                secondStarted.TrySetResult();
                return second;
            });

            var service = CreateService(processRunner, out _);
            await service.ConnectAsync();
            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));

            first.Exit(1, "connection closed by remote host");
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForStateAsync(service, TunnelState.Connected);

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));
            Assert.That(starts, Is.EqualTo(2));
        }

        [Test]
        public async Task It_faults_after_exhausting_reconnect_attempts()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            var session = new FakeProcessSession();
            SetupStartSession(processRunner, () => session);

            var service = CreateService(processRunner, out _);
            await service.ConnectAsync();
            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));

            // The remote becomes unreachable, so every reconnect fails — after the bounded attempts it faults.
            SetupRemoteKill(processRunner, exitCode: 255, standardError: "Connection refused");
            session.Exit(1, "connection closed by remote host");
            var status = await WaitForStateAsync(service, TunnelState.Faulted);

            Assert.That(status.Message, Does.Contain("reconnect"));
        }

        [Test]
        public async Task It_stops_reconnecting_when_the_user_disconnects()
        {
            var processRunner = new Mock<IProcessRunner>();
            SetupRemoteKill(processRunner);
            var session = new FakeProcessSession();
            SetupStartSession(processRunner, () => session);

            var service = CreateService(processRunner, out _);
            service.ReconnectBackoff = TimeSpan.FromSeconds(5);   // park the supervisor in its backoff
            await service.ConnectAsync();
            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Connected));

            session.Exit(1, "connection closed by remote host");
            await WaitForStateAsync(service, TunnelState.Reconnecting);
            await service.DisconnectAsync();

            Assert.That(service.Status.State, Is.EqualTo(TunnelState.Disconnected));
            processRunner.Verify(
                r => r.StartSession(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyDictionary<string, string>?>()),
                Times.Once);
        }
    }
}
