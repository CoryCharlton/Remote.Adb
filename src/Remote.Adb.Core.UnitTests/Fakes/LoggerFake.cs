using Microsoft.Extensions.Logging;

namespace Remote.Adb.Core.UnitTests.Fakes;

/// <summary>
/// A no-op <see cref="ILogger{T}"/> for tests that don't assert on logging.
/// </summary>
public sealed class LoggerFake<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
