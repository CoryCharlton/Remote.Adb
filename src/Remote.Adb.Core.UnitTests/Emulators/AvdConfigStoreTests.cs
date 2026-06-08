using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AvdConfigStoreTests
{
    private string _avdHome = string.Empty;
    private string? _previousAvdHome;

    [SetUp]
    public void SetUp()
    {
        _previousAvdHome = Environment.GetEnvironmentVariable("ANDROID_AVD_HOME");
        _avdHome = Path.Combine(Path.GetTempPath(), "remote-adb-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_avdHome);
        Environment.SetEnvironmentVariable("ANDROID_AVD_HOME", _avdHome);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("ANDROID_AVD_HOME", _previousAvdHome);

        if (Directory.Exists(_avdHome))
        {
            Directory.Delete(_avdHome, true);
        }
    }

    protected static AvdConfigStore CreateStore() => new(new LoggerFake<AvdConfigStore>());

    protected string WriteAvd(string folder, string config)
    {
        var directory = Path.Combine(_avdHome, folder + ".avd");
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.ini");
        File.WriteAllText(configPath, config);
        return configPath;
    }

    public class When_ReadAll_Is_Called : AvdConfigStoreTests
    {
        [Test]
        public void It_reads_the_configuration_of_every_avd()
        {
            WriteAvd("Pixel_6", "AvdId=Pixel_6\nhw.ramSize=2048\n");
            WriteAvd("Nexus_5", "AvdId=Nexus_5\nhw.ramSize=1024\n");

            var all = CreateStore().ReadAll();

            Assert.That(all.Select(configuration => configuration.AvdId), Is.EquivalentTo(new[] { "Pixel_6", "Nexus_5" }));
            Assert.That(all.Single(configuration => configuration.AvdId == "Pixel_6").RamSize, Is.EqualTo("2048"));
        }

        [Test]
        public void It_returns_empty_when_there_are_no_avds()
        {
            var all = CreateStore().ReadAll();

            Assert.That(all, Is.Empty);
        }
    }

    public class When_Write_Is_Called : AvdConfigStoreTests
    {
        [Test]
        public void It_updates_a_key_and_preserves_comments_and_unknown_keys()
        {
            var configPath = WriteAvd("Pixel_6", "# my avd\nAvdId=Pixel_6\nhw.ramSize=2048\nunknown.key=keep\n");

            var result = CreateStore().Write("Pixel_6",
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.RamSize, Is.EqualTo("4096"));
            Assert.That(
                File.ReadAllText(configPath),
                Is.EqualTo("# my avd\nAvdId=Pixel_6\nhw.ramSize=4096\nunknown.key=keep\n"));
        }

        [Test]
        public void It_removes_a_cleared_key()
        {
            var configPath = WriteAvd("Pixel_6", "AvdId=Pixel_6\nhw.gpu.mode=auto\nhw.ramSize=2048\n");

            var result = CreateStore().Write("Pixel_6",
                new Dictionary<string, string>(),
                new[] { "hw.gpu.mode" });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.GpuMode, Is.Null);
            Assert.That(File.ReadAllText(configPath), Is.EqualTo("AvdId=Pixel_6\nhw.ramSize=2048\n"));
        }

        [Test]
        public void It_returns_the_written_configuration_for_the_matching_avd()
        {
            WriteAvd("Pixel_6", "AvdId=Pixel_6\nhw.ramSize=2048\n");
            WriteAvd("Nexus_5", "AvdId=Nexus_5\nhw.ramSize=1024\n");

            var result = CreateStore().Write("Nexus_5",
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AvdId, Is.EqualTo("Nexus_5"));
            Assert.That(result.RamSize, Is.EqualTo("4096"));
        }

        [Test]
        public void It_returns_null_for_an_unknown_avd()
        {
            WriteAvd("Pixel_6", "AvdId=Pixel_6\n");

            var result = CreateStore().Write("Nexus_5",
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" });

            Assert.That(result, Is.Null);
        }
    }
}
