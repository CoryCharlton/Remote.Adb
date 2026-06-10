using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.UnitTests.Fakes;

/// <summary>Hands out a single <see cref="FakeDispatcherTimer"/> the test can drive.</summary>
public sealed class FakeTimerFactory : ITimerFactory
{
    public FakeDispatcherTimer Timer { get; } = new();

    public IDispatcherTimer Create(TimeSpan interval) => Timer;
}
