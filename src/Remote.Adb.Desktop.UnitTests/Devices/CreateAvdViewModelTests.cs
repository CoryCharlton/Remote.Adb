using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Moq;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Devices;

namespace Remote.Adb.Desktop.UnitTests.Devices;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class CreateAvdViewModelTests
{
    protected Mock<INotificationService> Notifications = null!;
    protected Mock<IAvdProvisioningService> Provisioning = null!;
    protected Mock<IAvdConfigStore> Store = null!;

    [SetUp]
    public void SetUp()
    {
        Provisioning = new Mock<IAvdProvisioningService>();
        Provisioning
            .Setup(service => service.ListInstalledImagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SystemImagePackage>());
        Provisioning
            .Setup(service => service.ListDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DeviceProfile>());

        Store = new Mock<IAvdConfigStore>();
        Notifications = new Mock<INotificationService>();
    }

    protected static AvdConfiguration Configuration(string avdId) =>
        new(avdId, IniParser.Parse(string.Empty), null);

    protected CreateAvdViewModel CreateViewModel() => new(Provisioning.Object, Store.Object, Notifications.Object);

    // A wizard primed past validation: a valid name plus a selected device and image.
    protected CreateAvdViewModel CreateReadyViewModel()
    {
        var viewModel = CreateViewModel();
        viewModel.Name = "Pixel_6";
        viewModel.SelectedDevice = new DeviceProfile("pixel_6", "Pixel 6", "Google");
        viewModel.SelectedImage = new SystemImagePackage("system-images;android-34;google_apis;x86_64", 34, "google_apis", "x86_64");
        return viewModel;
    }

    public class When_Finish_Is_Invoked_With_Invalid_Input : CreateAvdViewModelTests
    {
        [Test]
        public async Task It_rejects_an_invalid_name_without_creating()
        {
            var viewModel = CreateReadyViewModel();
            viewModel.Name = "bad name!";
            var closed = TrackClose(viewModel);

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(viewModel.StatusMessage, Is.Not.Null);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(0));
            Assert.That(closed.Value, Is.Null);
            Provisioning.Verify(
                service => service.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task It_requires_a_device()
        {
            var viewModel = CreateReadyViewModel();
            viewModel.SelectedDevice = null;

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(viewModel.StatusMessage, Is.EqualTo("Select a device."));
            Provisioning.Verify(
                service => service.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task It_requires_an_image()
        {
            var viewModel = CreateReadyViewModel();
            viewModel.SelectedImage = null;

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(viewModel.StatusMessage, Is.EqualTo("Select a system image."));
        }
    }

    public class When_Finish_Creates_The_Avd : CreateAvdViewModelTests
    {
        [Test]
        public async Task It_closes_with_success_when_the_config_is_saved()
        {
            Provisioning
                .Setup(service => service.CreateAsync("Pixel_6", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AvdOperationResult.Ok);
            Store
                .Setup(store => store.Write(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<IReadOnlyCollection<string>?>()))
                .Returns(Configuration("Pixel_6"));
            var viewModel = CreateReadyViewModel();
            var closed = TrackClose(viewModel);

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(closed.Value, Is.True);
            Notifications.Verify(
                notifications => notifications.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationSeverity>(), It.IsAny<TimeSpan?>(), It.IsAny<Action?>()),
                Times.Never);
        }

        [Test]
        public async Task It_closes_but_warns_when_the_config_cannot_be_saved()
        {
            Provisioning
                .Setup(service => service.CreateAsync("Pixel_6", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AvdOperationResult.Ok);
            Store
                .Setup(store => store.Write(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<IReadOnlyCollection<string>?>()))
                .Returns((AvdConfiguration?)null);
            var viewModel = CreateReadyViewModel();
            var closed = TrackClose(viewModel);

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(closed.Value, Is.True);
            Notifications.Verify(
                notifications => notifications.Show("Emulator created", It.IsAny<string>(), NotificationSeverity.Error, TimeSpan.Zero, It.IsAny<Action?>()),
                Times.Once);
        }

        [Test]
        public async Task It_reports_a_create_failure_without_closing()
        {
            Provisioning
                .Setup(service => service.CreateAsync("Pixel_6", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AvdOperationResult.Fail("avdmanager said no"));
            var viewModel = CreateReadyViewModel();
            var closed = TrackClose(viewModel);

            await viewModel.FinishCommand.ExecuteAsync(null);

            Assert.That(viewModel.StatusMessage, Is.EqualTo("avdmanager said no"));
            Assert.That(closed.Value, Is.Null);
            Store.Verify(
                store => store.Write(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<IReadOnlyCollection<string>?>()),
                Times.Never);
        }
    }

    protected static StrongBox<bool?> TrackClose(CreateAvdViewModel viewModel)
    {
        var closed = new StrongBox<bool?>(null);
        viewModel.CloseRequested += result => closed.Value = result;
        return closed;
    }
}
