using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AvdConfigWriterTests
{
    public class When_Write_Is_Called : AvdConfigWriterTests
    {
        [Test]
        public void It_round_trips_unchanged_input()
        {
            var text = "# An AVD\nAvdId=Pixel_6\nhw.ramSize=2048\n";

            var result = AvdConfigWriter.Write(IniParser.Parse(text), new Dictionary<string, string>());

            Assert.That(result, Is.EqualTo(text));
        }

        [Test]
        public void It_updates_only_the_changed_key_in_place()
        {
            var text = "AvdId=Pixel_6\nhw.ramSize=2048\nhw.gpu.mode=auto\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" });

            Assert.That(result, Is.EqualTo("AvdId=Pixel_6\nhw.ramSize=4096\nhw.gpu.mode=auto\n"));
        }

        [Test]
        public void It_preserves_comments_and_unknown_keys()
        {
            var text = "# keep me\nAvdId=Pixel_6\nunknown.key=value\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string> { ["AvdId"] = "Pixel_7" });

            Assert.That(result, Is.EqualTo("# keep me\nAvdId=Pixel_7\nunknown.key=value\n"));
        }

        [Test]
        public void It_appends_new_keys_in_sorted_order()
        {
            var text = "AvdId=Pixel_6\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string> { ["hw.ramSize"] = "2048", ["abi.type"] = "x86_64" });

            Assert.That(result, Is.EqualTo("AvdId=Pixel_6\nabi.type=x86_64\nhw.ramSize=2048\n"));
        }

        [Test]
        public void It_drops_only_the_removed_key()
        {
            var text = "# keep me\nAvdId=Pixel_6\nhw.gpu.mode=auto\nunknown.key=value\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string>(),
                new[] { "hw.gpu.mode" });

            Assert.That(result, Is.EqualTo("# keep me\nAvdId=Pixel_6\nunknown.key=value\n"));
        }

        [Test]
        public void It_ignores_removal_of_an_absent_key()
        {
            var text = "AvdId=Pixel_6\nhw.ramSize=2048\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string>(),
                new[] { "hw.gpu.mode" });

            Assert.That(result, Is.EqualTo(text));
        }

        [Test]
        public void It_sets_a_key_that_also_appears_in_removals()
        {
            var text = "AvdId=Pixel_6\nhw.ramSize=2048\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" },
                new[] { "hw.ramSize" });

            Assert.That(result, Is.EqualTo("AvdId=Pixel_6\nhw.ramSize=4096\n"));
        }

        [Test]
        public void It_updates_the_first_duplicate_key_and_drops_the_rest()
        {
            var text = "AvdId=Pixel_6\nhw.ramSize=2048\nhw.ramSize=1024\n";

            var result = AvdConfigWriter.Write(
                IniParser.Parse(text),
                new Dictionary<string, string> { ["hw.ramSize"] = "4096" });

            Assert.That(result, Is.EqualTo("AvdId=Pixel_6\nhw.ramSize=4096\n"));
        }
    }
}
