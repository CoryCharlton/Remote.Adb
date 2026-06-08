using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class IniParserTests
{
    public class When_Parse_Is_Called : IniParserTests
    {
        [Test]
        public void It_classifies_pairs_comments_and_blanks()
        {
            var document = IniParser.Parse("# comment\n\nAvdId=Pixel_6\n");

            Assert.That(
                document.Lines.Select(line => line.Kind),
                Is.EqualTo(new[] { IniLineKind.Comment, IniLineKind.Blank, IniLineKind.Pair }));
        }

        [Test]
        public void It_splits_on_the_first_equals()
        {
            var document = IniParser.Parse("hw.gpu.mode=auto=on\n");

            Assert.That(document.Get("hw.gpu.mode"), Is.EqualTo("auto=on"));
        }

        [Test]
        public void It_strips_carriage_returns()
        {
            var document = IniParser.Parse("AvdId=Pixel_6\r\nhw.ramSize=2048\r\n");

            Assert.That(document.Get("AvdId"), Is.EqualTo("Pixel_6"));
            Assert.That(document.Get("hw.ramSize"), Is.EqualTo("2048"));
        }

        [Test]
        public void It_preserves_line_order_and_count()
        {
            var document = IniParser.Parse("a=1\nb=2\nc=3\n");

            Assert.That(document.Lines.Count, Is.EqualTo(3));
            Assert.That(document.Lines.Select(line => line.Key), Is.EqualTo(new[] { "a", "b", "c" }));
        }
    }
}
