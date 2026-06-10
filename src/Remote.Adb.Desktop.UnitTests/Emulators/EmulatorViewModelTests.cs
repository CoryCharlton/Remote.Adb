using System.Diagnostics.CodeAnalysis;
using Moq;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class EmulatorViewModelTests
{
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

        Notifications = new Mock<INotificationService>();
        TimerFactory = new FakeTimerFactory();
    }

    protected static AndroidVirtualDevice Device(string name, bool running = false) =>
        new(name, name, null, running, running ? "emulator-5554" : null);

    protected EmulatorViewModel CreateViewModel() => new(
        EmulatorService.Object,
        Mock.Of<IAvdConfigStore>(),
        Mock.Of<IAvdCreateDialog>(),
        (_, _) => null!,
        Mock.Of<IAvdProvisioningService>(),
        Mock.Of<IConfirmDialog>(),
        Notifications.Object,
        TimerFactory);

    public class When_RefreshCommand_Is_Executed : EmulatorViewModelTests
    {
        [Test]
        public async Task It_orders_rows_by_display_name_case_insensitively()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Device("Zebra"), Device("alpha"), Device("Mango") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(
                viewModel.Emulators.Select(row => row.DisplayName),
                Is.EqualTo(new[] { "alpha", "Mango", "Zebra" }));
        }

        [Test]
        public async Task It_reconciles_rows_in_place_preserving_identity_and_starting_state()
        {
            EmulatorService
                .SetupSequence(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Device("A"), Device("B") })
                .ReturnsAsync(new[] { Device("B"), Device("C") });
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);
            var bRow = viewModel.Emulators.Single(row => row.Name == "B");
            bRow.IsStarting = true;

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.Emulators.Select(row => row.Name), Is.EqualTo(new[] { "B", "C" }));
            Assert.That(viewModel.Emulators.Single(row => row.Name == "B"), Is.SameAs(bRow));
            Assert.That(bRow.IsStarting, Is.True);
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
                    "Couldn't load emulators",
                    It.IsAny<string>(),
                    NotificationSeverity.Error,
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<Action?>()),
                Times.Once);
        }
    }

    public class When_IsListEmpty_Is_Read : EmulatorViewModelTests
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

        [Test]
        public async Task It_is_false_after_a_failed_load()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("emulator", new InvalidOperationException()));
            var viewModel = CreateViewModel();

            await viewModel.RefreshCommand.ExecuteAsync(null);

            Assert.That(viewModel.IsListEmpty, Is.False);
        }
    }

    public class When_Activated : EmulatorViewModelTests
    {
        [Test]
        public async Task It_refreshes_immediately_and_starts_the_timer()
        {
            var viewModel = CreateViewModel();

            await viewModel.OnActivatedAsync();

            EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(TimerFactory.Timer.IsRunning, Is.True);
        }
    }

    public class When_Deactivated : EmulatorViewModelTests
    {
        [Test]
        public async Task It_stops_the_timer()
        {
            var viewModel = CreateViewModel();
            await viewModel.OnActivatedAsync();

            viewModel.OnDeactivated();

            Assert.That(TimerFactory.Timer.IsRunning, Is.False);
            Assert.That(TimerFactory.Timer.StopCount, Is.EqualTo(1));
        }
    }

    public class When_The_Refresh_Timer_Ticks : EmulatorViewModelTests
    {
        [Test]
        public void It_refreshes()
        {
            var viewModel = CreateViewModel();

            TimerFactory.Timer.RaiseTick();

            EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task It_skips_while_a_row_is_starting()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Device("A") });
            var viewModel = CreateViewModel();
            await viewModel.RefreshCommand.ExecuteAsync(null);
            viewModel.Emulators.Single().IsStarting = true;

            TimerFactory.Timer.RaiseTick();

            EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void It_does_not_toast_on_a_silent_background_failure()
        {
            EmulatorService
                .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("emulator", new InvalidOperationException()));
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
