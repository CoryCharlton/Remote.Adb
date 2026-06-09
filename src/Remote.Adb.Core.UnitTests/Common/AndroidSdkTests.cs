using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.UnitTests.Fakes;

namespace Remote.Adb.Core.UnitTests.Common;

// Mutates process-wide environment variables, so it must not run alongside other fixtures.
[NonParallelizable]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AndroidSdkTests
{
    private string? _previousAndroidHome;
    private string? _previousAndroidSdkRoot;
    private string? _previousJavaHome;
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _previousAndroidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        _previousAndroidSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        _previousJavaHome = Environment.GetEnvironmentVariable("JAVA_HOME");

        Environment.SetEnvironmentVariable("ANDROID_HOME", null);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", null);
        Environment.SetEnvironmentVariable("JAVA_HOME", null);

        _tempRoot = Path.Combine(Path.GetTempPath(), "remote-adb-sdk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("ANDROID_HOME", _previousAndroidHome);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", _previousAndroidSdkRoot);
        Environment.SetEnvironmentVariable("JAVA_HOME", _previousJavaHome);

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    private static AndroidSdk CreateSdk(ISettingsService settings) => new(settings, new LoggerFake<AndroidSdk>());

    private static ISettingsService Settings(string? sdkRoot = null, string? javaHome = null)
    {
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.SdkRoot).Returns(sdkRoot);
        settings.SetupGet(s => s.JavaHome).Returns(javaHome);
        return settings.Object;
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public class When_Resolving_The_Sdk_Root : AndroidSdkTests
    {
        [Test]
        public void It_falls_back_to_the_environment_variable()
        {
            var envRoot = CreateDirectory("env");
            Environment.SetEnvironmentVariable("ANDROID_HOME", envRoot);

            var sdk = CreateSdk(Settings());

            Assert.That(sdk.SdkRoot, Is.EqualTo(envRoot));
            Assert.That(sdk.SdkRootSource, Is.EqualTo(SdkRootSource.EnvironmentVariable));
        }

        [Test]
        public void It_ignores_a_nonexistent_override()
        {
            var envRoot = CreateDirectory("env");
            Environment.SetEnvironmentVariable("ANDROID_HOME", envRoot);

            var sdk = CreateSdk(Settings(sdkRoot: Path.Combine(_tempRoot, "does-not-exist")));

            Assert.That(sdk.SdkRoot, Is.EqualTo(envRoot));
            Assert.That(sdk.SdkRootSource, Is.EqualTo(SdkRootSource.EnvironmentVariable));
        }

        [Test]
        public void It_prefers_the_override_over_the_environment_variable()
        {
            var overrideRoot = CreateDirectory("override");
            Environment.SetEnvironmentVariable("ANDROID_HOME", CreateDirectory("env"));

            var sdk = CreateSdk(Settings(sdkRoot: overrideRoot));

            Assert.That(sdk.SdkRoot, Is.EqualTo(overrideRoot));
            Assert.That(sdk.SdkRootSource, Is.EqualTo(SdkRootSource.Override));
        }
    }

    public class When_Resolving_Java_Home : AndroidSdkTests
    {
        [Test]
        public void It_falls_back_to_the_environment_variable()
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", "/env/jdk");

            var sdk = CreateSdk(Settings());

            Assert.That(sdk.JavaHome, Is.EqualTo("/env/jdk"));
        }

        [Test]
        public void It_is_null_when_neither_the_override_nor_the_environment_is_set()
        {
            var sdk = CreateSdk(Settings());

            Assert.That(sdk.JavaHome, Is.Null);
        }

        [Test]
        public void It_prefers_the_override()
        {
            Environment.SetEnvironmentVariable("JAVA_HOME", "/env/jdk");

            var sdk = CreateSdk(Settings(javaHome: "/override/jdk"));

            Assert.That(sdk.JavaHome, Is.EqualTo("/override/jdk"));
        }
    }
}
