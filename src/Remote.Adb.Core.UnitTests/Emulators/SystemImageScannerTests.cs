using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Emulators;

namespace Remote.Adb.Core.UnitTests.Emulators;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SystemImageScannerTests
{
    public class When_Scan_Is_Called : SystemImageScannerTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "remote-adb-sdk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private void CreateImage(string api, string tag, string abi) =>
            Directory.CreateDirectory(Path.Combine(_root, "system-images", api, tag, abi));

        [Test]
        public void It_builds_packages_for_each_abi_directory_newest_api_first()
        {
            CreateImage("android-33", "google_apis_playstore", "arm64-v8a");
            CreateImage("android-34", "google_apis", "x86_64");

            var images = SystemImageScanner.Scan(_root);

            Assert.That(images.Select(i => i.Package), Is.EqualTo(new[]
            {
                "system-images;android-34;google_apis;x86_64",
                "system-images;android-33;google_apis_playstore;arm64-v8a",
            }));
            Assert.That(images[0].ApiLevel, Is.EqualTo(34));
            Assert.That(images[0].Tag, Is.EqualTo("google_apis"));
            Assert.That(images[0].Abi, Is.EqualTo("x86_64"));
        }

        [Test]
        public void It_includes_minor_version_api_directories()
        {
            CreateImage("android-36.1", "google_apis_playstore", "x86_64");

            var images = SystemImageScanner.Scan(_root);

            Assert.That(images, Has.Count.EqualTo(1));
            Assert.That(images[0].Package, Is.EqualTo("system-images;android-36.1;google_apis_playstore;x86_64"));
            Assert.That(images[0].ApiLevel, Is.EqualTo(36));
        }

        [Test]
        public void It_skips_non_numeric_api_directories()
        {
            CreateImage("android-TiramisuPrivacySandbox", "google_apis", "x86_64");
            CreateImage("android-34", "google_apis", "x86_64");

            var images = SystemImageScanner.Scan(_root);

            Assert.That(images, Has.Count.EqualTo(1));
            Assert.That(images[0].ApiLevel, Is.EqualTo(34));
        }

        [Test]
        public void It_returns_empty_when_no_system_images_directory()
        {
            Assert.That(SystemImageScanner.Scan(_root), Is.Empty);
        }

        [Test]
        public void It_returns_empty_for_a_null_root()
        {
            Assert.That(SystemImageScanner.Scan(null), Is.Empty);
        }
    }
}
