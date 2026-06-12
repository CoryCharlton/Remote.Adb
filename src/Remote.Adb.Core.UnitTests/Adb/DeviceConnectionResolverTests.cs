using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Adb;

namespace Remote.Adb.Core.UnitTests.Adb;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DeviceConnectionResolverTests
{
    public class When_Resolve_Is_Called : DeviceConnectionResolverTests
    {
        [Test]
        public void It_classifies_an_emulator_serial()
        {
            Assert.That(DeviceConnectionResolver.Resolve("emulator-5554"), Is.EqualTo(DeviceConnection.Emulator));
        }

        [Test]
        public void It_classifies_a_host_port_serial_as_wireless()
        {
            Assert.That(DeviceConnectionResolver.Resolve("192.168.1.50:5555"), Is.EqualTo(DeviceConnection.Wireless));
        }

        [Test]
        public void It_classifies_an_mdns_paired_serial_as_wireless()
        {
            Assert.That(DeviceConnectionResolver.Resolve("adb-39281749-AbCdEf._adb-tls-connect._tcp"), Is.EqualTo(DeviceConnection.Wireless));
        }

        [Test]
        public void It_classifies_a_hardware_serial_as_usb()
        {
            Assert.That(DeviceConnectionResolver.Resolve("R5CW123ABCD"), Is.EqualTo(DeviceConnection.Usb));
        }
    }
}
