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

    private static DeviceService CreateService(Mock<IProcessRunner> processRunner)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AdbPath).Returns(AdbPath);

        return new DeviceService(processRunner.Object, sdk.Object, new LoggerFake<DeviceService>());
    }

    public class When_ListAsync_Is_Called : DeviceServiceTests
    {
        [Test]
        public async Task It_runs_adb_devices_long_and_parses_the_output()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, "List of devices attached\nemulator-5554\tdevice model:Pixel_7 transport_id:1\n", string.Empty));

            var service = CreateService(processRunner);

            var devices = await service.ListAsync();

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "devices", "-l" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.That(devices.Single().Model, Is.EqualTo("Pixel_7"));
        }
    }
}
