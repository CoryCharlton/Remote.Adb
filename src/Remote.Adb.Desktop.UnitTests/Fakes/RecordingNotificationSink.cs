using Remote.Adb.Desktop.Common.Notifications;

namespace Remote.Adb.Desktop.UnitTests.Fakes;

/// <summary>An <see cref="INotificationSink"/> that records every request it receives, in order.</summary>
public sealed class RecordingNotificationSink : INotificationSink
{
    public List<NotificationRequest> Requests { get; } = [];

    public void Show(NotificationRequest request) => Requests.Add(request);
}
