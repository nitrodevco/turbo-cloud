using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EvalHarness;

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>An ILogger that keeps every entry so a test can assert on levels.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));

    public IEnumerable<LogEntry> AtLeast(LogLevel level) => Entries.Where(e => e.Level >= level);
}
