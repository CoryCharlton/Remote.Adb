using System.Diagnostics.CodeAnalysis;
using Moq;
using NUnit.Framework;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;

namespace Remote.Adb.Core.UnitTests.Adb;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class DeviceDetailsResolverTests
{
    private const string AdbPath = "adb";

    private static DeviceDetailsResolver CreateResolver(Mock<IProcessRunner> processRunner)
    {
        var sdk = new Mock<IAndroidSdk>();
        sdk.SetupGet(s => s.AdbPath).Returns(AdbPath);

        return new DeviceDetailsResolver(processRunner.Object, sdk.Object);
    }

    public class When_ResolveAsync_Is_Called : DeviceDetailsResolverTests
    {
        [Test]
        public async Task It_reads_properties_and_builds_details()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, "ro.product.marketing.name=Pixel 9\n", string.Empty));

            var details = await CreateResolver(processRunner).ResolveAsync("ABC123");

            Assert.That(details!.Name, Is.EqualTo("Pixel 9"));
        }

        [Test]
        public async Task It_caches_by_serial_and_does_not_re_query()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, "ro.product.model=SM-S921B\n", string.Empty));
            var resolver = CreateResolver(processRunner);

            await resolver.ResolveAsync("ABC123");
            await resolver.ResolveAsync("ABC123");

            processRunner.Verify(
                r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task It_returns_null_when_the_shell_exits_nonzero()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(1, string.Empty, "error: device offline"));

            var details = await CreateResolver(processRunner).ResolveAsync("ABC123");

            Assert.That(details, Is.Null);
        }

        [Test]
        public async Task It_returns_null_when_adb_cannot_be_launched()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcessLaunchException("adb", new InvalidOperationException()));

            var details = await CreateResolver(processRunner).ResolveAsync("ABC123");

            Assert.That(details, Is.Null);
        }
    }
}
