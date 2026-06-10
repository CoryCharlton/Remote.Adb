namespace Remote.Adb.Desktop.Common.Threading;

/// <inheritdoc />
public sealed class DispatcherTimerFactory : ITimerFactory
{
    /// <inheritdoc />
    public IDispatcherTimer Create(TimeSpan interval) => new DispatcherTimerAdapter(interval);
}
