using System.Diagnostics.CodeAnalysis;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.UnitTests.Fakes;

namespace Remote.Adb.Desktop.UnitTests.Common.Notifications;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class NotificationServiceTests
{
    protected static NotificationService CreateService() => new();

    public class When_Show_Is_Called_Before_A_Sink_Is_Attached : NotificationServiceTests
    {
        [Test]
        public void It_buffers_and_flushes_in_order_once_a_sink_attaches()
        {
            var service = CreateService();
            var sink = new RecordingNotificationSink();

            service.Show("a", "1", NotificationSeverity.Information);
            service.Show("b", "2", NotificationSeverity.Error);

            Assert.That(sink.Requests, Is.Empty);

            service.SetSink(sink);

            Assert.That(sink.Requests.Select(request => request.Title), Is.EqualTo(new[] { "a", "b" }));
        }
    }

    public class When_Show_Is_Called_With_A_Sink_Attached : NotificationServiceTests
    {
        [Test]
        public void It_forwards_immediately_carrying_every_field()
        {
            var service = CreateService();
            var sink = new RecordingNotificationSink();
            service.SetSink(sink);

            Action onClick = () => { };
            service.Show("title", "message", NotificationSeverity.Warning, TimeSpan.FromSeconds(5), onClick);

            var request = sink.Requests.Single();
            Assert.That(request.Title, Is.EqualTo("title"));
            Assert.That(request.Message, Is.EqualTo("message"));
            Assert.That(request.Severity, Is.EqualTo(NotificationSeverity.Warning));
            Assert.That(request.Expiration, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That((object?)request.OnClick, Is.SameAs(onClick));
        }
    }

    public class When_SetSink_Is_Called_With_Null : NotificationServiceTests
    {
        [Test]
        public void It_detaches_so_later_notifications_buffer_again()
        {
            var service = CreateService();
            service.SetSink(new RecordingNotificationSink());
            service.SetSink(null);

            service.Show("buffered", "again", NotificationSeverity.Information);

            var next = new RecordingNotificationSink();
            service.SetSink(next);

            Assert.That(next.Requests.Single().Title, Is.EqualTo("buffered"));
        }
    }
}
