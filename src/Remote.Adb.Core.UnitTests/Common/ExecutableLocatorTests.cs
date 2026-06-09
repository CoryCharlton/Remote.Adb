using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.UnitTests.Common;

// Mutates the PATH environment variable, so it must not run alongside other fixtures.
[NonParallelizable]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class ExecutableLocatorTests
{
    private string _directory = string.Empty;
    private string? _previousPath;

    [SetUp]
    public void SetUp()
    {
        _previousPath = Environment.GetEnvironmentVariable("PATH");
        _directory = Path.Combine(Path.GetTempPath(), "remote-adb-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable("PATH", _directory);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("PATH", _previousPath);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    public class When_FindOnPath_Is_Called : ExecutableLocatorTests
    {
        [Test]
        public void It_finds_an_executable_on_the_path()
        {
            // On Windows FindOnPath probes the executable extensions for a bare name; create one it will match.
            var fileName = OperatingSystem.IsWindows() ? "mytool.bat" : "mytool";
            var expected = Path.Combine(_directory, fileName);
            File.WriteAllText(expected, string.Empty);

            Assert.That(ExecutableLocator.FindOnPath("mytool"), Is.EqualTo(expected));
        }

        [Test]
        public void It_returns_null_when_the_executable_is_not_on_the_path()
        {
            Assert.That(ExecutableLocator.FindOnPath("definitely-not-a-real-tool"), Is.Null);
        }
    }
}
