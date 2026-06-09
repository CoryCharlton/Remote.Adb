using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Diagnostics;

namespace Remote.Adb.Core.UnitTests.Diagnostics;

// Controls whether java is on PATH, so it must not run alongside other fixtures.
[NonParallelizable]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class SdkDiagnosticsTests
{
    private string _directory = string.Empty;
    private string? _previousPath;

    [SetUp]
    public void SetUp()
    {
        _previousPath = Environment.GetEnvironmentVariable("PATH");
        _directory = Path.Combine(Path.GetTempPath(), "remote-adb-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        // Isolate the java probe: PATH contains only our temp dir (no java) unless a test adds one.
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

    private static SdkDiagnostics CreateDiagnostics(SdkRootSource source = SdkRootSource.Override, string? sdkRoot = "/sdk", string? javaHome = null)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.SdkRootSource).Returns(source);
        sdk.SetupGet(s => s.SdkRoot).Returns(sdkRoot);
        sdk.SetupGet(s => s.JavaHome).Returns(javaHome);

        return new SdkDiagnostics(sdk.Object);
    }

    private string CreateFakeJdk()
    {
        var home = Path.Combine(_directory, "jdk-" + Guid.NewGuid().ToString("N"));
        var executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "bin", executable), string.Empty);
        return home;
    }

    private void PutJavaOnPath()
    {
        var executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
        File.WriteAllText(Path.Combine(_directory, executable), string.Empty);
    }

    public class When_Evaluate_Is_Called : SdkDiagnosticsTests
    {
        [Test]
        public void It_reports_an_error_when_the_sdk_is_not_found()
        {
            var diagnostics = CreateDiagnostics(SdkRootSource.NotFound, sdkRoot: null, javaHome: CreateFakeJdk());

            var issue = diagnostics.Evaluate().Single();

            Assert.That(issue.Title, Is.EqualTo("Android SDK"));
            Assert.That(issue.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void It_reports_an_error_when_no_jdk_is_available()
        {
            var diagnostics = CreateDiagnostics(javaHome: null);

            var issue = diagnostics.Evaluate().Single();

            Assert.That(issue.Title, Is.EqualTo("JDK"));
            Assert.That(issue.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void It_reports_an_error_when_java_home_is_set_but_has_no_java()
        {
            var brokenJdk = Path.Combine(_directory, "broken-jdk");
            Directory.CreateDirectory(brokenJdk);
            PutJavaOnPath();

            var diagnostics = CreateDiagnostics(javaHome: brokenJdk);

            var issue = diagnostics.Evaluate().Single();

            Assert.That(issue.Title, Is.EqualTo("JDK"));
            Assert.That(issue.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void It_reports_nothing_when_the_sdk_and_jdk_are_explicit()
        {
            var diagnostics = CreateDiagnostics(SdkRootSource.EnvironmentVariable, javaHome: CreateFakeJdk());

            Assert.That(diagnostics.Evaluate(), Is.Empty);
        }

        [Test]
        public void It_warns_when_java_is_only_on_the_path()
        {
            PutJavaOnPath();
            var diagnostics = CreateDiagnostics(javaHome: null);

            var issue = diagnostics.Evaluate().Single();

            Assert.That(issue.Title, Is.EqualTo("JDK"));
            Assert.That(issue.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }

        [Test]
        public void It_warns_when_the_sdk_is_only_the_default_guess()
        {
            var diagnostics = CreateDiagnostics(SdkRootSource.DefaultFallback, javaHome: CreateFakeJdk());

            var issue = diagnostics.Evaluate().Single();

            Assert.That(issue.Title, Is.EqualTo("Android SDK"));
            Assert.That(issue.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }
    }
}
