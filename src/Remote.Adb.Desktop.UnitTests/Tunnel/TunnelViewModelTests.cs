using System.Diagnostics.CodeAnalysis;
using Moq;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.Tunnel;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Tunnel;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Tunnel;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class TunnelViewModelTests
{
    protected Mock<IAdbServerService> AdbServer = null!;
    protected Mock<INotificationService> Notifications = null!;
    protected Mock<ISettingsService> Settings = null!;
    protected TunnelStatus Status = null!;
    protected Mock<ITunnelService> Tunnel = null!;

    [SetUp]
    public void SetUp()
    {
        Status = new TunnelStatus(TunnelState.Disconnected, null);

        Tunnel = new Mock<ITunnelService>();
        Tunnel.SetupGet(t => t.Status).Returns(() => Status);
        Tunnel.Setup(t => t.ConnectAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Tunnel.Setup(t => t.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AdbServer = new Mock<IAdbServerService>();
        Notifications = new Mock<INotificationService>();
        Settings = new Mock<ISettingsService>();
    }

    protected TunnelViewModel CreateViewModel(string? host = "devhost")
    {
        Settings.SetupGet(s => s.TunnelHost).Returns(host);
        Settings.SetupGet(s => s.TunnelRemotePort).Returns(5037);
        Settings.SetupGet(s => s.TunnelLocalPort).Returns(5037);
        Settings.SetupGet(s => s.TunnelAutoConnect).Returns(true);

        return new TunnelViewModel(Tunnel.Object, AdbServer.Object, Settings.Object, new FakeUiDispatcher(), Notifications.Object);
    }

    public class When_ConnectCommand : TunnelViewModelTests
    {
        [Test]
        public async Task It_delegates_to_the_service()
        {
            var viewModel = CreateViewModel();

            await viewModel.ConnectCommand.ExecuteAsync(null);

            Tunnel.Verify(t => t.ConnectAsync("devhost", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void It_is_disabled_when_the_host_is_blank()
        {
            var viewModel = CreateViewModel(host: null);

            Assert.That(viewModel.ConnectCommand.CanExecute(null), Is.False);
        }
    }

    public class When_A_Setting_Is_Edited : TunnelViewModelTests
    {
        [Test]
        public void It_persists_the_host()
        {
            var viewModel = CreateViewModel();

            viewModel.RemoteHost = "newhost";

            Settings.VerifySet(s => s.TunnelHost = "newhost", Times.Once);
        }

        [Test]
        public void It_persists_the_ports_and_auto_connect()
        {
            var viewModel = CreateViewModel();

            viewModel.RemotePort = 6000;
            viewModel.LocalPort = 6001;
            viewModel.AutoConnect = false;

            Settings.VerifySet(s => s.TunnelRemotePort = 6000, Times.Once);
            Settings.VerifySet(s => s.TunnelLocalPort = 6001, Times.Once);
            Settings.VerifySet(s => s.TunnelAutoConnect = false, Times.Once);
        }

        [Test]
        public void It_clamps_an_out_of_range_port_and_persists_the_clamped_value()
        {
            var viewModel = CreateViewModel();

            viewModel.RemotePort = 70000;

            Assert.That(viewModel.RemotePort, Is.EqualTo(65535));
            Settings.VerifySet(s => s.TunnelRemotePort = 65535, Times.Once);
        }
    }

    public class When_The_Status_Changes : TunnelViewModelTests
    {
        [Test]
        public async Task It_reflects_the_new_state_after_activation()
        {
            var viewModel = CreateViewModel();
            await viewModel.OnActivatedAsync();

            Status = new TunnelStatus(TunnelState.Connected, "Forwarding devhost:5037 → 127.0.0.1:5037");
            Tunnel.Raise(t => t.StatusChanged += null, Tunnel.Object, Status);

            Assert.That(viewModel.IsConnected, Is.True);
            Assert.That(viewModel.StateText, Is.EqualTo("Connected"));
            Assert.That(viewModel.CanDisconnect, Is.True);
        }
    }

    public class When_RestartAdbServerCommand : TunnelViewModelTests
    {
        [Test]
        public async Task It_shows_an_error_toast_when_the_restart_fails()
        {
            AdbServer
                .Setup(a => a.RestartAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("adb", new Exception("boom")));
            var viewModel = CreateViewModel();

            await viewModel.RestartAdbServerCommand.ExecuteAsync(null);

            Notifications.Verify(
                n => n.Show("Couldn't restart adb", It.IsAny<string>(), NotificationSeverity.Error, It.IsAny<TimeSpan?>(), It.IsAny<Action?>()),
                Times.Once);
        }
    }
}
