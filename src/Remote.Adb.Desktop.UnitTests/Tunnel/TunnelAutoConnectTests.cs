using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.Tunnel;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop.UnitTests.Tunnel;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class TunnelAutoConnectTests
{
    protected Mock<ISettingsService> Settings = null!;
    protected Mock<ITunnelService> Tunnel = null!;

    [SetUp]
    public void SetUp()
    {
        Tunnel = new Mock<ITunnelService>();
        Tunnel.Setup(t => t.ConnectAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Settings = new Mock<ISettingsService>();
    }

    private async Task RunAsync(TunnelAutoConnect service)
    {
        await ((IHostedService)service).StartAsync(CancellationToken.None);
        await ((IHostedService)service).StopAsync(CancellationToken.None);
    }

    private TunnelAutoConnect CreateService() =>
        new(Tunnel.Object, Settings.Object, Mock.Of<ILogger<TunnelAutoConnect>>());

    public class When_Started : TunnelAutoConnectTests
    {
        [Test]
        public async Task It_connects_when_enabled_and_a_host_is_configured()
        {
            Settings.SetupGet(s => s.TunnelAutoConnect).Returns(true);
            Settings.SetupGet(s => s.TunnelHost).Returns("devhost");

            await RunAsync(CreateService());

            Tunnel.Verify(t => t.ConnectAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task It_does_nothing_when_auto_connect_is_disabled()
        {
            Settings.SetupGet(s => s.TunnelAutoConnect).Returns(false);
            Settings.SetupGet(s => s.TunnelHost).Returns("devhost");

            await RunAsync(CreateService());

            Tunnel.Verify(t => t.ConnectAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task It_does_nothing_when_no_host_is_configured()
        {
            Settings.SetupGet(s => s.TunnelAutoConnect).Returns(true);
            Settings.SetupGet(s => s.TunnelHost).Returns((string?)null);

            await RunAsync(CreateService());

            Tunnel.Verify(t => t.ConnectAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
