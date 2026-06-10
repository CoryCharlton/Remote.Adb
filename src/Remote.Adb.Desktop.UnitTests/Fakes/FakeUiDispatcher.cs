using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.UnitTests.Fakes;

/// <summary>An <see cref="IUiDispatcher"/> that runs posted actions synchronously, inline.</summary>
public sealed class FakeUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
