using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Core.UnitTests.Adb;

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

    public class When_ParseDeviceList_Is_Called
    {
        [Test]
        public void It_captures_the_descriptive_columns_of_online_devices()
        {
            var output = "List of devices attached\n"
                + "emulator-5554          device product:sdk_gphone64 model:sdk_gphone64_x86_64 device:emu64xa transport_id:1\n";

            var devices = AdbOutputParser.ParseDeviceList(output);

            var device = devices.Single();
            Assert.That(device.Serial, Is.EqualTo("emulator-5554"));
            Assert.That(device.State, Is.EqualTo("device"));
            Assert.That(device.IsOnline, Is.True);
            Assert.That(device.Model, Is.EqualTo("sdk_gphone64_x86_64"));
            Assert.That(device.Product, Is.EqualTo("sdk_gphone64"));
            Assert.That(device.Device, Is.EqualTo("emu64xa"));
            Assert.That(device.TransportId, Is.EqualTo("1"));
        }

        [Test]
        public void It_keeps_devices_that_are_not_online()
        {
            var output = "List of devices attached\n"
                + "0A1B2C3D               unauthorized\n"
                + "192.168.1.5:5555       offline\n";

            var devices = AdbOutputParser.ParseDeviceList(output);

            Assert.That(devices.Select(d => d.Serial), Is.EqualTo(new[] { "0A1B2C3D", "192.168.1.5:5555" }));
            Assert.That(devices.All(d => !d.IsOnline), Is.True);
            Assert.That(devices[0].State, Is.EqualTo("unauthorized"));
            Assert.That(devices[0].Model, Is.Null);
        }

        [Test]
        public void It_returns_empty_when_no_devices_attached()
        {
            var devices = AdbOutputParser.ParseDeviceList("List of devices attached\n\n");

            Assert.That(devices, Is.Empty);
        }
    }
}
