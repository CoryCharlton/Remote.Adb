using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.UnitTests.Fakes;

/// <summary>An <see cref="IDispatcherTimer"/> whose ticks a test raises synchronously via <see cref="RaiseTick"/>.</summary>
public sealed class FakeDispatcherTimer : IDispatcherTimer
{
    public event EventHandler? Tick;

    public bool IsRunning { get; private set; }

    public int StopCount { get; private set; }

    public void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);

    public void Start() => IsRunning = true;

    public void Stop()
    {
        IsRunning = false;
        StopCount++;
    }
}
