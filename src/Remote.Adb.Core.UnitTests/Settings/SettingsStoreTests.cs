using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Settings;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SettingsStoreTests
{
    private string _directory = string.Empty;
    private string _filePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "remote-adb-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private SettingsStore CreateStore(string? filePath = null) =>
        new(new LoggerFake<SettingsStore>(), filePath ?? _filePath);

    public class When_Load_Is_Called : SettingsStoreTests
    {
        [Test]
        public void It_returns_defaults_when_the_file_is_missing()
        {
            var settings = CreateStore().Load();

            Assert.That(settings.Theme, Is.EqualTo(AppTheme.Dark));
            Assert.That(settings.Density, Is.EqualTo(AppDensity.Compact));
        }

        [Test]
        public void It_returns_defaults_when_the_file_is_corrupt()
        {
            File.WriteAllText(_filePath, "{ not json");

            var settings = CreateStore().Load();

            Assert.That(settings.Theme, Is.EqualTo(AppTheme.Dark));
            Assert.That(settings.Density, Is.EqualTo(AppDensity.Compact));
        }

        [Test]
        public void It_returns_defaults_when_the_content_is_null()
        {
            File.WriteAllText(_filePath, "null");

            var settings = CreateStore().Load();

            Assert.That(settings.Theme, Is.EqualTo(AppTheme.Dark));
        }

        [Test]
        public void It_round_trips_a_saved_model()
        {
            CreateStore().Save(new SettingsModel { Theme = AppTheme.Light, Density = AppDensity.Normal });

            var settings = CreateStore().Load();

            Assert.That(settings.Theme, Is.EqualTo(AppTheme.Light));
            Assert.That(settings.Density, Is.EqualTo(AppDensity.Normal));
        }

        [Test]
        public void It_reads_enums_written_as_strings()
        {
            File.WriteAllText(_filePath, "{\"theme\":\"Light\",\"density\":\"Normal\"}");

            var settings = CreateStore().Load();

            Assert.That(settings.Theme, Is.EqualTo(AppTheme.Light));
            Assert.That(settings.Density, Is.EqualTo(AppDensity.Normal));
        }

        [Test]
        public void It_preserves_unknown_keys_across_a_resave()
        {
            File.WriteAllText(_filePath, "{\"theme\":\"Light\",\"futureKey\":123}");
            var store = CreateStore();

            store.Save(store.Load());

            Assert.That(File.ReadAllText(_filePath), Does.Contain("futureKey"));
        }
    }

    public class When_Save_Is_Called : SettingsStoreTests
    {
        [Test]
        public void It_creates_the_directory_when_it_does_not_exist()
        {
            var nestedPath = Path.Combine(_directory, "nested", "settings.json");

            CreateStore(nestedPath).Save(new SettingsModel());

            Assert.That(File.Exists(nestedPath), Is.True);
        }

        [Test]
        public void It_writes_enums_as_strings()
        {
            CreateStore().Save(new SettingsModel { Theme = AppTheme.Light, Density = AppDensity.Normal });

            var json = File.ReadAllText(_filePath);
            Assert.That(json, Does.Contain("\"Light\""));
            Assert.That(json, Does.Contain("\"Normal\""));
        }

        [Test]
        public void It_overwrites_without_leaving_a_temp_file()
        {
            var store = CreateStore();
            store.Save(new SettingsModel { Theme = AppTheme.Dark });
            store.Save(new SettingsModel { Theme = AppTheme.Light });

            Assert.That(CreateStore().Load().Theme, Is.EqualTo(AppTheme.Light));
            Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
        }
    }
}
