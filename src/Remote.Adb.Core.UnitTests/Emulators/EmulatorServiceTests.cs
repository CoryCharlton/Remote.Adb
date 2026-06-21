using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class EmulatorServiceTests
{
    private const string AdbPath = "adb";
    private const string EmulatorPath = "emulator";

    private static EmulatorService CreateService(Mock<IProcessRunner> processRunner, IReadOnlyDictionary<string, AvdMetadata>? catalog = null)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AdbPath).Returns(AdbPath);
        sdk.SetupGet(s => s.EmulatorPath).Returns(EmulatorPath);

        var avdCatalog = new Mock<IAvdCatalog>();
        avdCatalog.Setup(c => c.Read()).Returns(catalog ?? new Dictionary<string, AvdMetadata>());

        return new EmulatorService(processRunner.Object, sdk.Object, avdCatalog.Object, new LoggerFake<EmulatorService>());
    }

    public class When_ListAsync_Is_Called
    {
        [Test]
        public async Task It_marks_running_avds_with_their_serial()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .Returns((string _, IReadOnlyList<string> arguments, string? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                {
                    if (arguments.Contains("-list-avds"))
                    {
                        return Task.FromResult(new ProcessResult(0, "Pixel_6_API_34\nNexus_5\n", string.Empty));
                    }

                    if (arguments is ["devices"])
                    {
                        return Task.FromResult(new ProcessResult(0, "List of devices attached\nemulator-5554\tdevice\n", string.Empty));
                    }

                    if (arguments.Contains("avd") && arguments.Contains("name"))
                    {
                        return Task.FromResult(new ProcessResult(0, "Pixel_6_API_34\nOK\n", string.Empty));
                    }

                    return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
                });

            var service = CreateService(processRunner);

            var devices = await service.ListAsync();

            var running = devices.Single(device => device.Name == "Pixel_6_API_34");
            var stopped = devices.Single(device => device.Name == "Nexus_5");

            Assert.That(devices, Has.Count.EqualTo(2));
            Assert.That(running.IsRunning, Is.True);
            Assert.That(running.Serial, Is.EqualTo("emulator-5554"));
            Assert.That(stopped.IsRunning, Is.False);
            Assert.That(stopped.Serial, Is.Null);
        }

        [Test]
        public async Task It_uses_the_catalog_display_name_and_falls_back_to_the_id()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .Returns((string _, IReadOnlyList<string> arguments, string? _, IReadOnlyDictionary<string, string>? _, CancellationToken _) =>
                {
                    if (arguments.Contains("-list-avds"))
                    {
                        return Task.FromResult(new ProcessResult(0, "Pixel_6_API_34\nNexus_5\n", string.Empty));
                    }

                    return Task.FromResult(new ProcessResult(0, "List of devices attached\n", string.Empty));
                });

            var catalog = new Dictionary<string, AvdMetadata>
            {
                ["Pixel_6_API_34"] = new("Pixel_6_API_34", "Pixel 6 API 34", "Phone"),
            };

            var service = CreateService(processRunner, catalog);

            var devices = await service.ListAsync();

            var pixel = devices.Single(device => device.Name == "Pixel_6_API_34");
            var nexus = devices.Single(device => device.Name == "Nexus_5");

            Assert.That(pixel.DisplayName, Is.EqualTo("Pixel 6 API 34"));
            Assert.That(pixel.Tag, Is.EqualTo("Phone"));
            Assert.That(nexus.DisplayName, Is.EqualTo("Nexus_5"));
            Assert.That(nexus.Tag, Is.Null);
        }
    }

    public class When_StartAsync_Is_Called
    {
        [Test]
        public async Task It_appends_no_snapshot_load_when_cold_booting()
        {
            var processRunner = new Mock<IProcessRunner>();

            var service = CreateService(processRunner);

            await service.StartAsync("Pixel_6_API_34", coldBoot: true);

            processRunner.Verify(
                r => r.Start(EmulatorPath, It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(new[] { "-avd", "Pixel_6_API_34", "-no-snapshot-load" }))),
                Times.Once);
        }

        [Test]
        public async Task It_launches_the_emulator_with_the_avd_name()
        {
            var processRunner = new Mock<IProcessRunner>();

            var service = CreateService(processRunner);

            await service.StartAsync("Pixel_6_API_34");

            processRunner.Verify(
                r => r.Start(EmulatorPath, It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(new[] { "-avd", "Pixel_6_API_34" }))),
                Times.Once);
        }
    }

    public class When_StopAsync_Is_Called
    {
        [Test]
        public async Task It_invokes_adb_emu_kill_for_the_serial()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, string.Empty, string.Empty));

            var service = CreateService(processRunner);

            await service.StopAsync("emulator-5554");

            processRunner.Verify(
                r => r.RunAsync(AdbPath, It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(new[] { "-s", "emulator-5554", "emu", "kill" })), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
