using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Settings;

namespace Remote.Adb.Core.UnitTests.Settings;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SettingsServiceTests
{
    public class When_A_Setting_Is_Changed : SettingsServiceTests
    {
        [Test]
        public void It_persists_a_changed_theme_via_the_store()
        {
            var store = new SettingsStoreFake();
            var service = new SettingsService(store);

            service.Theme = AppTheme.Light;

            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Saved!.Theme, Is.EqualTo(AppTheme.Light));
        }

        [Test]
        public void It_persists_a_changed_density_via_the_store()
        {
            var store = new SettingsStoreFake();
            var service = new SettingsService(store);

            service.Density = AppDensity.Normal;

            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.Saved!.Density, Is.EqualTo(AppDensity.Normal));
        }

        [Test]
        public void It_does_not_save_when_the_value_is_unchanged()
        {
            var store = new SettingsStoreFake { Model = new SettingsModel { Theme = AppTheme.Dark } };
            var service = new SettingsService(store);

            service.Theme = AppTheme.Dark;

            Assert.That(store.SaveCount, Is.EqualTo(0));
        }
    }

    public class When_Constructed : SettingsServiceTests
    {
        [Test]
        public void It_loads_the_persisted_settings_from_the_store()
        {
            var store = new SettingsStoreFake
            {
                Model = new SettingsModel { Theme = AppTheme.Light, Density = AppDensity.Normal },
            };

            var service = new SettingsService(store);

            Assert.That(service.Theme, Is.EqualTo(AppTheme.Light));
            Assert.That(service.Density, Is.EqualTo(AppDensity.Normal));
        }
    }

    private sealed class SettingsStoreFake : ISettingsStore
    {
        public SettingsModel Model { get; set; } = new();

        public SettingsModel? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public SettingsModel Load() => Model;

        public void Save(SettingsModel settings)
        {
            Saved = settings;
            SaveCount++;
        }
    }
}
