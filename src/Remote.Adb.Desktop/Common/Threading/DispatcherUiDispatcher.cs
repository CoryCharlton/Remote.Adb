using Avalonia.Threading;

namespace Remote.Adb.Desktop.Common.Threading;

/// <inheritdoc />
public sealed class DispatcherUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
