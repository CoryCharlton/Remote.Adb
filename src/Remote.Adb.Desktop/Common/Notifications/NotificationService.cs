using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace Remote.Adb.Desktop.Common.Notifications;

/// <inheritdoc />
public sealed class NotificationService : INotificationService
{
    private readonly List<Notification> _pending = [];
    private WindowNotificationManager? _manager;

    /// <summary>
    /// Attaches (or, with <see langword="null"/>, clears) the sink that renders notifications — set by the shell
    /// window once it opens. Any notifications raised before the sink attached are flushed now.
    /// </summary>
    public void SetNotificationManager(WindowNotificationManager? manager)
    {
        _manager = manager;

        if (manager is null || _pending.Count == 0)
        {
            return;
        }

        foreach (var notification in _pending)
        {
            manager.Show(notification);
        }

        _pending.Clear();
    }

    /// <inheritdoc />
    public void Show(string title, string message, NotificationSeverity severity, TimeSpan? expiration = null, Action? onClick = null)
    {
        // Marshal to the UI thread; this also keeps _pending single-threaded (only ever touched here and in
        // SetNotificationManager, both on the UI thread).
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(title, message, severity, expiration, onClick));
            return;
        }

        var notification = new Notification(title, message, ToNotificationType(severity), expiration, onClick);

        if (_manager is null)
        {
            _pending.Add(notification);
            return;
        }

        _manager.Show(notification);
    }

    private static NotificationType ToNotificationType(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => NotificationType.Success,
        NotificationSeverity.Warning => NotificationType.Warning,
        NotificationSeverity.Error => NotificationType.Error,
        _ => NotificationType.Information,
    };
}
