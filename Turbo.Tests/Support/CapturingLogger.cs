using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Turbo.Tests.Support;

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// An ILogger that keeps every entry so a test can assert on levels. Without
/// <paramref name="keepExceptions"/> it keeps their text only, as a server's log does: an error
/// thrown in a plugin's code holds the plugin's assembly context while it is kept.
/// </summary>
public sealed class CapturingLogger<T>(bool keepExceptions) : ILogger<T>
{
    // Built by type too (Activator), which needs a constructor without parameters.
    public CapturingLogger()
        : this(keepExceptions: true) { }

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
    ) =>
        Entries.Enqueue(
            new LogEntry(logLevel, formatter(state, exception), keepExceptions ? exception : null)
        );

    public IEnumerable<LogEntry> AtLeast(LogLevel level) => Entries.Where(e => e.Level >= level);
}
