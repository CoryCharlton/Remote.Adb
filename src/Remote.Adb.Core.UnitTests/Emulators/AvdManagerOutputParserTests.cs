using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AvdManagerOutputParserTests
{
    public class When_ParseDevices_Is_Called : AvdManagerOutputParserTests
    {
        [Test]
        public void It_parses_id_name_and_oem_per_block()
        {
            var output =
                "Available devices definitions:\n"
                + "id: 0 or \"automotive_1024p_landscape\"\n"
                + "    Name: Automotive (1024p landscape)\n"
                + "    OEM : Google\n"
                + "---------\n"
                + "id: 9 or \"pixel_6\"\n"
                + "    Name: Pixel 6\n"
                + "    OEM : Google\n"
                + "    Tag : google_apis_playstore\n"
                + "---------\n";

            var devices = AvdManagerOutputParser.ParseDevices(output);

            Assert.That(devices.Select(d => d.Id), Is.EqualTo(new[] { "automotive_1024p_landscape", "pixel_6" }));
            Assert.That(devices[1].Name, Is.EqualTo("Pixel 6"));
            Assert.That(devices[1].Oem, Is.EqualTo("Google"));
            Assert.That(devices[1].Tag, Is.EqualTo("google_apis_playstore"));
            Assert.That(devices[0].Tag, Is.Null);
        }

        [Test]
        public void It_leaves_oem_null_when_absent()
        {
            var output = "id: 2 or \"tv_1080p\"\n    Name: Television (1080p)\n---------\n";

            var devices = AvdManagerOutputParser.ParseDevices(output);

            Assert.That(devices, Has.Count.EqualTo(1));
            Assert.That(devices[0].Id, Is.EqualTo("tv_1080p"));
            Assert.That(devices[0].Oem, Is.Null);
        }

        [Test]
        public void It_falls_back_to_the_numeric_id_when_unquoted()
        {
            var output = "id: 5\n    Name: Custom\n";

            var devices = AvdManagerOutputParser.ParseDevices(output);

            Assert.That(devices[0].Id, Is.EqualTo("5"));
        }

        [Test]
        public void It_returns_empty_for_no_devices()
        {
            Assert.That(AvdManagerOutputParser.ParseDevices("Available devices definitions:\n"), Is.Empty);
        }
    }
}
