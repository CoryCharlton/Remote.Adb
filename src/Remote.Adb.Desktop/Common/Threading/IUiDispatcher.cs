namespace Remote.Adb.Desktop.Common.Threading;

/// <summary>
/// Marshals an action onto the UI thread. Lets a view model react to events raised on background threads
/// (e.g. a service status change) without depending on Avalonia's <c>Dispatcher</c> directly.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
    void Post(Action action);
}
