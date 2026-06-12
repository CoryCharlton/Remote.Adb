using System.Diagnostics.CodeAnalysis;
using Moq;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Devices;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DevicesViewModelTests
{
    protected Mock<IDeviceService> DeviceService = null!;
    protected Mock<IEmulatorService> EmulatorService = null!;
    protected Mock<INotificationService> Notifications = null!;
    protected FakeTimerFactory TimerFactory = null!;

    [SetUp]
    public void SetUp()
    {
        EmulatorService = new Mock<IEmulatorService>();
        EmulatorService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AndroidVirtualDevice>());

        DeviceService = new Mock<IDeviceService>();
        DeviceService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AdbDevice>());

        Notifications = new Mock<INotificationService>();
        TimerFactory = new FakeTimerFactory();
    }

    protected static AdbDevice Physical(string serial, string? model = null, string state = "device") =>
        new(serial, state, model, null, null, null);

    protected static AndroidVirtualDevice Virtual(string name, bool running = false) =>
        new(name, name, null, running, running ? "emulator-5554" : null);

    protected DevicesViewModel CreateViewModel() => new(
        EmulatorService.Object,
        DeviceService.Object,
        Mock.Of<IAvdConfigStore>(),
        Mock.Of<IAvdCreateDialog>(),
        (_, _) => null!,
        Mock.Of<IAvdProvisioningService>(),
        Mock.Of<IConfirmDialog>(),
        Notifications.Object,
        TimerFactory);

    public class When_Activated : DevicesViewModelTests
    {
        [Test]
        public async Task It_lists_both_sources_and_starts_the_refresh_timer()
        {
            var viewModel = CreateViewModel();

            await viewModel.OnActivatedAsync();

            EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        public async Task It_merges_virtual_and_physical_devices_into_one_list()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("Mango") });
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Physical("s1", "Zebra"), Physical("s2", "alpha") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.DisplayName), Is.EqualTo(new[] { "alpha", "Mango", "Zebra" }));
        }

        [Test]
        public async Task It_excludes_running_emulators_from_the_physical_list()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("Pixel", running: true) });
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Physical("emulator-5554"), Physical("ABC123", "Phone") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.IdentityKey), Does.Not.Contain("dev:emulator-5554"));
            Assert.That(viewModel.Devices.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task It_orders_virtual_rows_by_display_name_case_insensitively()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("Zebra"), Virtual("alpha"), Virtual("Mango") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.DisplayName), Is.EqualTo(new[] { "alpha", "Mango", "Zebra" }));
        }

        [Test]
        public async Task It_reconciles_virtual_rows_in_place_preserving_identity_and_starting_state()
        {
            EmulatorService
                .SetupSequence(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("A"), Virtual("B") })
                .ReturnsAsync(new[] { Virtual("B"), Virtual("C") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);
            var bRow = viewModel.Devices.Single(row => row.Name == "B");
            bRow.IsStarting = true;

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.Name), Is.EqualTo(new[] { "B", "C" }));
            Assert.That(viewModel.Devices.Single(row => row.Name == "B"), Is.SameAs(bRow));
            Assert.That(bRow.IsStarting, Is.True);
        }

        [Test]
        public async Task It_updates_physical_rows_in_place_across_refreshes()
        {
            DeviceService
                .SetupSequence(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Physical("s1", "Pixel", state: "offline") })
                .ReturnsAsync(new[] { Physical("s1", "Pixel", state: "device") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);
            var row = viewModel.Devices.Single();
            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Single(), Is.SameAs(row));
            Assert.That(row.IsOnline, Is.True);
        }

        [Test]
        public async Task It_does_not_drop_physical_rows_when_the_avd_set_changes()
        {
            EmulatorService
                .SetupSequence(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("A") })
                .ReturnsAsync(Array.Empty<AndroidVirtualDevice>());
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Physical("s1", "Phone") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);
            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Devices.Select(row => row.IdentityKey), Is.EqualTo(new[] { "dev:s1" }));
        }

        [Test]
        public async Task It_shows_an_error_toast_when_the_list_fails()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("emulator", new InvalidOperationException()));
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Notifications.Verify(
                notifications => notifications.Show(
                    "Couldn't list devices",
                    It.IsAny<string>(),
                    NotificationSeverity.Error,
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<Action?>()),
                Times.Once);
        }
    }

    public class When_IsListEmpty_Is_Read : DevicesViewModelTests
    {
        [Test]
        public void It_is_false_before_the_first_load()
        {
            Assert.That(CreateViewModel().IsListEmpty, Is.False);
        }

        [Test]
        public async Task It_is_true_after_a_successful_empty_load()
        {
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.IsListEmpty, Is.True);
        }
    }

    public class When_The_Refresh_Timer_Ticks : DevicesViewModelTests
    {
        [Test]
        public void It_refreshes()
        {
            var viewModel = CreateViewModel();

            TimerFactory.Timer.RaiseTick();

            DeviceService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task It_skips_while_a_virtual_row_is_starting()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Virtual("A") });
            var viewModel = CreateViewModel();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.Devices.Single().IsStarting = true;

            TimerFactory.Timer.RaiseTick();

            EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void It_does_not_toast_on_a_silent_background_failure()
        {
            DeviceService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("adb", new InvalidOperationException()));
            var viewModel = CreateViewModel();

            TimerFactory.Timer.RaiseTick();

            Notifications.Verify(
                notifications => notifications.Show(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<NotificationSeverity>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<Action?>()),
                Times.Never);
        }
    }
}
