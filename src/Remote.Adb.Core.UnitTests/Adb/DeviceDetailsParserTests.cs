using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Core.UnitTests.Adb;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DeviceDetailsParserTests
{
    public class When_Build_Is_Called : DeviceDetailsParserTests
    {
        [Test]
        public void It_prefers_the_marketing_name_for_a_physical_device()
        {
            var output = "ro.product.marketing.name=Galaxy S24\nro.product.model=SM-S921B\n";

            var details = DeviceDetailsParser.Build("R5CW123", output);

            Assert.That(details.Name, Is.EqualTo("Galaxy S24"));
            Assert.That(details.IsEmulator, Is.False);
        }

        [Test]
        public void It_falls_back_to_the_model_when_no_marketing_name()
        {
            var output = "ro.product.model=SM-S921B\n";

            var details = DeviceDetailsParser.Build("R5CW123", output);

            Assert.That(details.Name, Is.EqualTo("SM-S921B"));
        }

        [Test]
        public void It_names_an_emulator_after_its_avd_and_unslugs_underscores()
        {
            var output = "ro.kernel.qemu=1\nro.boot.qemu.avd_name=Pixel_9\nro.product.model=sdk_gphone64_x86_64\n";

            var details = DeviceDetailsParser.Build("emulator-5554", output);

            Assert.That(details.Name, Is.EqualTo("Pixel 9"));
            Assert.That(details.IsEmulator, Is.True);
        }

        [Test]
        public void It_detects_an_emulator_from_the_serial_alone()
        {
            var details = DeviceDetailsParser.Build("emulator-5556", string.Empty);

            Assert.That(details.IsEmulator, Is.True);
        }

        [Test]
        public void It_derives_the_form_factor_from_characteristics()
        {
            var output = "ro.build.characteristics=nosdcard,watch\n";

            var details = DeviceDetailsParser.Build("R5CW123", output);

            Assert.That(details.Form, Is.EqualTo(DeviceForm.Watch));
        }

        [Test]
        public void It_reads_the_api_level_and_abi()
        {
            var output = "ro.build.version.sdk=35\nro.product.cpu.abi=arm64-v8a\n";

            var details = DeviceDetailsParser.Build("R5CW123", output);

            Assert.That(details.ApiLevel, Is.EqualTo(35));
            Assert.That(details.Abi, Is.EqualTo("arm64-v8a"));
        }

        [Test]
        public void It_defaults_to_a_phone_and_a_null_name()
        {
            var details = DeviceDetailsParser.Build("R5CW123", string.Empty);

            Assert.That(details.Form, Is.EqualTo(DeviceForm.Phone));
            Assert.That(details.Name, Is.Null);
        }
    }
}
