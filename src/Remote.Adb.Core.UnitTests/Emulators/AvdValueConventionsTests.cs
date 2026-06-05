using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AvdValueConventionsTests
{
    public class When_IsValidSize_Is_Called : AvdValueConventionsTests
    {
        [TestCase("2048")]
        [TestCase("512M")]
        [TestCase("2G")]
        [TestCase("4GB")]
        [TestCase("256m")]
        [TestCase(" 1024 ")]
        public void It_accepts_a_bare_number_or_unit_suffix(string value)
        {
            Assert.That(AvdValueConventions.IsValidSize(value), Is.True);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("abc")]
        [TestCase("2.5G")]
        [TestCase("-1")]
        [TestCase("2T")]
        public void It_rejects_blank_or_malformed_values(string value)
        {
            Assert.That(AvdValueConventions.IsValidSize(value), Is.False);
        }
    }

    public class When_IsValidCount_Is_Called : AvdValueConventionsTests
    {
        [TestCase("1")]
        [TestCase("4")]
        [TestCase(" 8 ")]
        public void It_accepts_a_positive_whole_number(string value)
        {
            Assert.That(AvdValueConventions.IsValidCount(value), Is.True);
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("-2")]
        [TestCase("2.5")]
        [TestCase("x")]
        public void It_rejects_non_positive_or_non_integer_values(string value)
        {
            Assert.That(AvdValueConventions.IsValidCount(value), Is.False);
        }
    }

    public class When_IsValidAvdName_Is_Called : AvdValueConventionsTests
    {
        [TestCase("Pixel_6")]
        [TestCase("Test-AVD.1")]
        [TestCase("tv_1080p")]
        public void It_accepts_filesystem_safe_names(string value)
        {
            Assert.That(AvdValueConventions.IsValidAvdName(value), Is.True);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Pixel 6")]
        [TestCase("bad/name")]
        [TestCase("oops!")]
        public void It_rejects_blank_or_unsafe_names(string value)
        {
            Assert.That(AvdValueConventions.IsValidAvdName(value), Is.False);
        }
    }
}
