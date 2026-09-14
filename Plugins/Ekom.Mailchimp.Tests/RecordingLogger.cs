using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Mailchimp.Tests;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<string> Errors { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Error)
        {
            Errors.Enqueue(formatter(state, exception));
        }
    }
}
