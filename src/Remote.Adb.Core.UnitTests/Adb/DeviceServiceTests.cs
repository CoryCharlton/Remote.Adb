using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Adb;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DeviceServiceTests
{
    private const string AdbPath = "adb";

    private static DeviceService CreateService(Mock<IProcessRunner> processRunner, Mock<IDeviceDetailsResolver>? detailsResolver = null)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AdbPath).Returns(AdbPath);

        detailsResolver ??= new Mock<IDeviceDetailsResolver>();

        return new DeviceService(processRunner.Object, sdk.Object, detailsResolver.Object, new LoggerFake<DeviceService>());
    }

    private static Mock<IProcessRunner> ProcessRunnerReturning(string standardOutput)
    {
        var processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, standardOutput, string.Empty));

        return processRunner;
    }

    public class When_ListAsync_Is_Called : DeviceServiceTests
    {
        [Test]
        public async Task It_runs_adb_devices_long_and_parses_the_output()
        {
            var processRunner = ProcessRunnerReturning("List of devices attached\nemulator-5554\tdevice model:Pixel_7 transport_id:1\n");
            var service = CreateService(processRunner);

            var devices = await service.ListAsync();

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "devices", "-l" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.That(devices.Single().Model, Is.EqualTo("Pixel_7"));
        }

        [Test]
        public async Task It_applies_resolved_details_to_online_devices()
        {
            var processRunner = ProcessRunnerReturning("List of devices attached\nABC123\tdevice transport_id:1\n");
            var detailsResolver = new Mock<IDeviceDetailsResolver>();
            detailsResolver
                .Setup(resolver => resolver.ResolveAsync("ABC123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeviceDetails("Pixel 9", DeviceForm.Phone, false, 35, "arm64-v8a"));
            var service = CreateService(processRunner, detailsResolver);

            var device = (await service.ListAsync()).Single();

            Assert.That(device.Name, Is.EqualTo("Pixel 9"));
            Assert.That(device.Form, Is.EqualTo(DeviceForm.Phone));
            Assert.That(device.IsEmulator, Is.False);
            Assert.That(device.ApiLevel, Is.EqualTo(35));
            Assert.That(device.Abi, Is.EqualTo("arm64-v8a"));
        }

        [Test]
        public async Task It_does_not_resolve_details_for_offline_devices()
        {
            var processRunner = ProcessRunnerReturning("List of devices attached\nABC123\toffline\n");
            var detailsResolver = new Mock<IDeviceDetailsResolver>();
            var service = CreateService(processRunner, detailsResolver);

            await service.ListAsync();

            detailsResolver.Verify(resolver => resolver.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
