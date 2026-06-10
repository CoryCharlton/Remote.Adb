using System.Diagnostics.CodeAnalysis;
using Moq;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Devices;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DevicesViewModelTests
{
    protected Mock<IDeviceService> DeviceService = null!;
    protected Mock<INotificationService> Notifications = null!;
    protected FakeTimerFactory TimerFactory = null!;

    [SetUp]
    public void SetUp()
    {
        DeviceService = new Mock<IDeviceService>();
        DeviceService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AdbDevice>());

        Notifications = new Mock<INotificationService>();
        TimerFactory = new FakeTimerFactory();
    }

    protected static AdbDevice Device(string serial, string? model = null, string state = "device") =>
        new(serial, state, model, null, null, null);

    protected DevicesViewModel CreateViewModel() => new(DeviceService.Object, Notifications.Object, TimerFactory);

    public class When_Activated : DevicesViewModelTests
    {
        [Test]
        public async Task It_lists_and_starts_the_refresh_timer()
        {
            var viewModel = CreateViewModel();

            await viewModel.OnActivatedAsync();

            DeviceService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(TimerFactory.Timer.IsRunning, Is.True);
        }

        [Test]
        public void It_stops_the_timer_on_deactivate()
        {
            var viewModel = CreateViewModel();

            viewModel.OnDeactivated();

            Assert.That(TimerFactory.Timer.StopCount, Is.EqualTo(1));
        }
    }

    public class When_RefreshCommand_Is_Executed : DevicesViewModelTests
    {
        [Test]
        public async Task It_orders_rows_by_display_name_case_insensitively()
        {
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Device("s1", "Zebra"), Device("s2", "alpha"), Device("s3", "Mango") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.DisplayName), Is.EqualTo(new[] { "alpha", "Mango", "Zebra" }));
        }

        [Test]
        public async Task It_updates_rows_in_place_across_refreshes()
        {
            DeviceService
                .SetupSequence(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Device("s1", "Pixel", state: "offline") })
                .ReturnsAsync(new[] { Device("s1", "Pixel", state: "device") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);
            var row = viewModel.Devices.Single();
            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Single(), Is.SameAs(row));
            Assert.That(row.IsOnline, Is.True);
        }

        [Test]
        public async Task It_shows_a_toast_when_a_user_initiated_refresh_fails()
        {
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("adb", new Exception("boom")));
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Notifications.Verify(
                n => n.Show("Couldn't list devices", It.IsAny<string>(), NotificationSeverity.Error, It.IsAny<TimeSpan?>(), It.IsAny<Action?>()),
                Times.Once);
        }
    }

    public class When_A_Background_Tick_Fails : DevicesViewModelTests
    {
        [Test]
        public async Task It_stays_silent()
        {
            await CreateViewModel().OnActivatedAsync();
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("adb", new Exception("boom")));

            TimerFactory.Timer.RaiseTick();

            Notifications.Verify(
                n => n.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationSeverity>(), It.IsAny<TimeSpan?>(), It.IsAny<Action?>()),
                Times.Never);
        }
    }
}
