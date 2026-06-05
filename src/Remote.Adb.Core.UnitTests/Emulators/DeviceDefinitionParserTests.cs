using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DeviceDefinitionParserTests
{
    private const string Xml =
        "<d:devices xmlns:d=\"http://schemas.android.com/sdk/devices/3\">"
        + "<d:device>"
        + "  <d:name>Pixel 6</d:name><d:id>pixel_6</d:id><d:manufacturer>Google</d:manufacturer>"
        + "  <d:hardware><d:screen>"
        + "    <d:screen-size>normal</d:screen-size><d:diagonal-length>6.40</d:diagonal-length>"
        + "    <d:pixel-density>420dpi</d:pixel-density>"
        + "    <d:dimensions><d:x-dimension>1080</d:x-dimension><d:y-dimension>2400</d:y-dimension></d:dimensions>"
        + "  </d:screen><d:ram unit=\"GiB\">2</d:ram></d:hardware>"
        + "  <d:software><d:api-level>24-</d:api-level></d:software>"
        + "  <d:playstore-enabled>true</d:playstore-enabled>"
        + "</d:device>"
        + "<d:device>"
        + "  <d:name>Medium Tablet</d:name><d:id>medium_tablet</d:id><d:manufacturer>Generic</d:manufacturer>"
        + "  <d:hardware><d:screen>"
        + "    <d:screen-size>xlarge</d:screen-size><d:diagonal-length>10.1</d:diagonal-length>"
        + "    <d:dimensions><d:x-dimension>1600</d:x-dimension><d:y-dimension>2560</d:y-dimension></d:dimensions>"
        + "  </d:screen></d:hardware>"
        + "</d:device>"
        + "<d:device deprecated=\"true\">"
        + "  <d:name>Television (4K)</d:name><d:id>tv_4k</d:id><d:manufacturer>Google</d:manufacturer>"
        + "  <d:hardware><d:screen><d:pixel-density>xhdpi</d:pixel-density>"
        + "    <d:dimensions><d:x-dimension>3840</d:x-dimension><d:y-dimension>2160</d:y-dimension></d:dimensions>"
        + "  </d:screen></d:hardware><d:tag-id>android-tv</d:tag-id>"
        + "</d:device>"
        + "</d:devices>";

    public class When_Parse_Is_Called : DeviceDefinitionParserTests
    {
        [Test]
        public void It_reads_id_name_oem_and_screen_specs()
        {
            var pixel = DeviceDefinitionParser.Parse(Xml).Single(device => device.Id == "pixel_6");

            Assert.That(pixel.Name, Is.EqualTo("Pixel 6"));
            Assert.That(pixel.Oem, Is.EqualTo("Google"));
            Assert.That(pixel.ScreenWidth, Is.EqualTo(1080));
            Assert.That(pixel.ScreenHeight, Is.EqualTo(2400));
            Assert.That(pixel.Density, Is.EqualTo(420));
            Assert.That(pixel.RamMb, Is.EqualTo(2048));
            Assert.That(pixel.MinApi, Is.EqualTo(24));
            Assert.That(pixel.SupportedApi, Is.EqualTo("24+"));
            Assert.That(pixel.PlayStore, Is.True);
            Assert.That(pixel.IsObsolete, Is.False);
        }

        [Test]
        public void It_flags_deprecated_devices_as_obsolete()
        {
            var tv = DeviceDefinitionParser.Parse(Xml).Single(device => device.Id == "tv_4k");

            Assert.That(tv.IsObsolete, Is.True);
            Assert.That(tv.PlayStore, Is.False);
        }

        [Test]
        public void It_classifies_form_factors()
        {
            var devices = DeviceDefinitionParser.Parse(Xml).ToDictionary(device => device.Id);

            Assert.That(devices["pixel_6"].FormFactor, Is.EqualTo("phone"));
            Assert.That(devices["medium_tablet"].FormFactor, Is.EqualTo("tablet"));
            Assert.That(devices["tv_4k"].FormFactor, Is.EqualTo("tv"));
        }

        [Test]
        public void It_computes_density_from_resolution_when_not_specified()
        {
            var tablet = DeviceDefinitionParser.Parse(Xml).Single(device => device.Id == "medium_tablet");

            // sqrt(1600^2 + 2560^2) / 10.1 ≈ 299
            Assert.That(tablet.Density, Is.EqualTo(299));
        }

        [Test]
        public void It_maps_density_buckets()
        {
            var tv = DeviceDefinitionParser.Parse(Xml).Single(device => device.Id == "tv_4k");

            Assert.That(tv.Density, Is.EqualTo(320)); // xhdpi
        }

        [Test]
        public void It_returns_empty_for_malformed_xml()
        {
            Assert.That(DeviceDefinitionParser.Parse("not xml <<<"), Is.Empty);
        }
    }
}
