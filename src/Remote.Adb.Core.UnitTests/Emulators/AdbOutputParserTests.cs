using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AdbOutputParserTests
{
    public class When_ParseDevices_Is_Called
    {
        [Test]
        public void It_returns_serials_of_online_devices()
        {
            var output = "List of devices attached\nemulator-5554\tdevice\n192.168.1.5:5555\tdevice\n";

            var serials = AdbOutputParser.ParseDevices(output);

            Assert.That(serials, Is.EqualTo(new[] { "emulator-5554", "192.168.1.5:5555" }));
        }

        [Test]
        public void It_skips_offline_and_unauthorized_devices()
        {
            var output = "List of devices attached\nemulator-5554\toffline\nemulator-5556\tunauthorized\nemulator-5558\tdevice\n";

            var serials = AdbOutputParser.ParseDevices(output);

            Assert.That(serials, Is.EqualTo(new[] { "emulator-5558" }));
        }

        [Test]
        public void It_returns_empty_when_no_devices_attached()
        {
            var output = "List of devices attached\n\n";

            var serials = AdbOutputParser.ParseDevices(output);

            Assert.That(serials, Is.Empty);
        }
    }
}
