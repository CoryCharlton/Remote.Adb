using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AvdProvisioningServiceTests
{
    private const string AvdManagerPath = "/sdk/cmdline-tools/latest/bin/avdmanager";

    private static AvdProvisioningService CreateService(Mock<IProcessRunner> processRunner, string? sdkRoot = null)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AvdManagerPath).Returns(AvdManagerPath);
        sdk.SetupGet(s => s.SdkRoot).Returns(sdkRoot);

        return new AvdProvisioningService(processRunner.Object, sdk.Object, new LoggerFake<AvdProvisioningService>());
    }

    public class When_CreateAsync_Is_Called : AvdProvisioningServiceTests
    {
        [Test]
        public async Task It_invokes_avdmanager_create_with_no_piped_to_stdin()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

            var service = CreateService(processRunner);

            var created = await service.CreateAsync("Test_AVD", "system-images;android-34;google_apis;x86_64", "pixel_6");

            Assert.That(created.Success, Is.True);
            processRunner.Verify(
                r => r.RunAsync(
                    AvdManagerPath,
                    It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[]
                    {
                        "create", "avd", "-n", "Test_AVD",
                        "-k", "system-images;android-34;google_apis;x86_64",
                        "-d", "pixel_6",
                    })),
                    "no\n",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task It_returns_failure_with_the_tool_output()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(1, string.Empty, "boom"));

            var created = await CreateService(processRunner).CreateAsync("Test_AVD", "pkg", "pixel_6");

            Assert.That(created.Success, Is.False);
            Assert.That(created.Error, Is.EqualTo("boom"));
        }

        [Test]
        public async Task It_surfaces_stdout_when_avdmanager_writes_the_error_there()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(1, "ERROR: JAVA_HOME is not set and no 'java' command could be found.", string.Empty));

            var created = await CreateService(processRunner).CreateAsync("Test_AVD", "pkg", "pixel_6");

            Assert.That(created.Success, Is.False);
            Assert.That(created.Error, Does.Contain("JAVA_HOME"));
        }
    }

    public class When_DeleteAsync_Is_Called : AvdProvisioningServiceTests
    {
        [Test]
        public async Task It_invokes_avdmanager_delete()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

            await CreateService(processRunner).DeleteAsync("Test_AVD");

            processRunner.Verify(
                r => r.RunAsync(
                    AvdManagerPath,
                    It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "delete", "avd", "-n", "Test_AVD" })),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    public class When_ListDevicesAsync_Is_Called : AvdProvisioningServiceTests
    {
        [Test]
        public async Task It_parses_avdmanager_list_device_output()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, "id: 9 or \"pixel_6\"\n    Name: Pixel 6\n    OEM : Google\n---------\n", string.Empty));

            var devices = await CreateService(processRunner).ListDevicesAsync();

            Assert.That(devices, Has.Count.EqualTo(1));
            Assert.That(devices[0].Id, Is.EqualTo("pixel_6"));
        }
    }

    public class When_ListInstalledImagesAsync_Is_Called : AvdProvisioningServiceTests
    {
        [Test]
        public async Task It_scans_the_sdk_root()
        {
            var root = Path.Combine(Path.GetTempPath(), "remote-adb-sdk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "system-images", "android-34", "google_apis", "x86_64"));

            try
            {
                var images = await CreateService(new Mock<IProcessRunner>(), root).ListInstalledImagesAsync();

                Assert.That(images, Has.Count.EqualTo(1));
                Assert.That(images[0].Package, Is.EqualTo("system-images;android-34;google_apis;x86_64"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
