using System.Diagnostics.CodeAnalysis;
using Moq;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Diagnostics;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.Settings;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Shell;
using Remote.Adb.Desktop.Theming;
using Remote.Adb.Desktop.Tunnel;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Shell;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class MainWindowViewModelTests
{
    protected Mock<ISdkDiagnostics> Diagnostics = null!;
    protected Mock<IEmulatorService> EmulatorService = null!;
    protected Mock<INotificationService> Notifications = null!;

    [SetUp]
    public void SetUp()
    {
        EmulatorService = new Mock<IEmulatorService>();
        EmulatorService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AndroidVirtualDevice>());

        Diagnostics = new Mock<ISdkDiagnostics>();
        Diagnostics.Setup(diagnostics => diagnostics.Evaluate()).Returns(Array.Empty<ToolDiagnostic>());

        Notifications = new Mock<INotificationService>();
    }

    protected MainWindowViewModel CreateViewModel()
    {
        var emulator = new EmulatorViewModel(
            EmulatorService.Object,
            Mock.Of<IAvdConfigStore>(),
            Mock.Of<IAvdCreateDialog>(),
            (_, _) => null!,
            Mock.Of<IAvdProvisioningService>(),
            Mock.Of<IConfirmDialog>(),
            Notifications.Object,
            new FakeTimerFactory());

        var settings = new SettingsViewModel(
            Mock.Of<ISettingsService>(),
            Mock.Of<IAndroidSdk>(),
            Mock.Of<IThemeApplier>(),
            Mock.Of<IDensityApplier>());

        return new MainWindowViewModel(
            emulator,
            new DevicesViewModel(),
            new TunnelViewModel(),
            settings,
            Diagnostics.Object,
            Notifications.Object);
    }

    protected void VerifyListCount(Times times) =>
        EmulatorService.Verify(service => service.ListAsync(It.IsAny<CancellationToken>()), times);

    public class When_RaiseStartupDiagnostics_Is_Called : MainWindowViewModelTests
    {
        [Test]
        public void It_shows_one_persistent_toast_per_diagnostic_with_the_mapped_severity()
        {
            Diagnostics
                .Setup(diagnostics => diagnostics.Evaluate())
                .Returns(new[]
                {
                    new ToolDiagnostic("Android SDK", "missing", DiagnosticSeverity.Error),
                    new ToolDiagnostic("JDK", "guessed", DiagnosticSeverity.Warning),
                });
            var viewModel = CreateViewModel();

            viewModel.RaiseStartupDiagnostics();

            Notifications.Verify(
                notifications => notifications.Show("Android SDK", "missing", NotificationSeverity.Error, TimeSpan.Zero, It.IsAny<Action?>()),
                Times.Once);
            Notifications.Verify(
                notifications => notifications.Show("JDK", "guessed", NotificationSeverity.Warning, TimeSpan.Zero, It.IsAny<Action?>()),
                Times.Once);
        }
    }

    public class When_The_Live_Screen_Changes : MainWindowViewModelTests
    {
        [Test]
        public void It_activates_the_emulator_screen_on_construction()
        {
            CreateViewModel();

            VerifyListCount(Times.Once());
        }

        [Test]
        public void It_reactivates_the_emulator_when_window_focus_returns()
        {
            var viewModel = CreateViewModel();

            viewModel.SetWindowActive(false);
            viewModel.SetWindowActive(true);

            VerifyListCount(Times.Exactly(2));
        }

        [Test]
        public void It_ignores_a_redundant_window_active()
        {
            var viewModel = CreateViewModel();

            viewModel.SetWindowActive(true);

            VerifyListCount(Times.Once());
        }

        [Test]
        public void It_deactivates_on_navigation_away_and_reactivates_on_return()
        {
            var viewModel = CreateViewModel();

            viewModel.SelectedDestination = viewModel.Destinations.First(destination => destination.Label == "Settings");
            VerifyListCount(Times.Once());

            viewModel.SelectedDestination = viewModel.Destinations.First(destination => destination.Label == "Emulators");
            VerifyListCount(Times.Exactly(2));
        }
    }
}
