using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class EmulatorOutputParserTests
{
    public class When_ParseAvdList_Is_Called
    {
        [Test]
        public void It_returns_one_name_per_nonblank_line()
        {
            var output = "Pixel_6_API_34\nNexus_5\n\n";

            var names = EmulatorOutputParser.ParseAvdList(output);

            Assert.That(names, Is.EqualTo(new[] { "Pixel_6_API_34", "Nexus_5" }));
        }

        [Test]
        public void It_returns_empty_when_no_avds()
        {
            var names = EmulatorOutputParser.ParseAvdList(string.Empty);

            Assert.That(names, Is.Empty);
        }
    }

    public class When_ParseAvdName_Is_Called
    {
        [Test]
        public void It_returns_the_name_before_the_status_line()
        {
            var output = "Pixel_6_API_34\nOK\n";

            var name = EmulatorOutputParser.ParseAvdName(output);

            Assert.That(name, Is.EqualTo("Pixel_6_API_34"));
        }

        [Test]
        public void It_returns_null_when_only_a_status_line_is_present()
        {
            var output = "OK\n";

            var name = EmulatorOutputParser.ParseAvdName(output);

            Assert.That(name, Is.Null);
        }
    }
}
