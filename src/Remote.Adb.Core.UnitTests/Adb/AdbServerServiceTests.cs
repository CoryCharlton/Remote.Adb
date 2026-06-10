using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Adb;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AdbServerServiceTests
{
    private const string AdbPath = "adb";

    private static AdbServerService CreateService(Mock<IProcessRunner> processRunner)
    {
        processRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AdbPath).Returns(AdbPath);

        return new AdbServerService(processRunner.Object, sdk.Object, new LoggerFake<AdbServerService>());
    }

    public class When_StartAsync_Is_Called : AdbServerServiceTests
    {
        [Test]
        public async Task It_runs_adb_start_server()
        {
            var processRunner = new Mock<IProcessRunner>();
            var service = CreateService(processRunner);

            await service.StartAsync();

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "start-server" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    public class When_KillAsync_Is_Called : AdbServerServiceTests
    {
        [Test]
        public async Task It_runs_adb_kill_server()
        {
            var processRunner = new Mock<IProcessRunner>();
            var service = CreateService(processRunner);

            await service.KillAsync();

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "kill-server" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    public class When_RestartAsync_Is_Called : AdbServerServiceTests
    {
        [Test]
        public async Task It_kills_then_starts_and_raises_ServerRestarted()
        {
            var processRunner = new Mock<IProcessRunner>();
            var service = CreateService(processRunner);

            var restarted = false;
            service.ServerRestarted += (_, _) => restarted = true;

            await service.RestartAsync();

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "kill-server" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "start-server" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.That(restarted, Is.True);
        }
    }

    public class When_IsRunningAsync_Is_Called : AdbServerServiceTests
    {
        [Test]
        public async Task It_reflects_whether_a_listener_is_bound_to_the_port()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var service = CreateService(new Mock<IProcessRunner>());

            var whileListening = await service.IsRunningAsync(port);
            listener.Stop();
            var afterStopped = await service.IsRunningAsync(port);

            Assert.That(whileListening, Is.True);
            Assert.That(afterStopped, Is.False);
        }

        [Test]
        public async Task It_returns_false_for_an_out_of_range_port()
        {
            var service = CreateService(new Mock<IProcessRunner>());

            Assert.That(await service.IsRunningAsync(70000), Is.False);
        }
    }
}
