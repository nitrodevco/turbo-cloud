using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Logging;

namespace Turbo.Logging;

/// <summary>
/// The server's latest log entries in memory (<see cref="IServerLog"/>): a logger provider beside
/// the console's, so it is given what the console is, by the same filters.
/// </summary>
[ProviderAlias("ServerLog")]
public sealed class ServerLog(int capacity = ServerLog.DEFAULT_CAPACITY, TimeProvider? clock = null)
    : ILoggerProvider,
        IServerLog
{
    public const int DEFAULT_CAPACITY = 2000;

    /// <summary>An exception's text is cut to this, its stack trace's top frames first.</summary>
    public const int MAX_EXCEPTION_CHARS = 8000;

    private readonly ServerLogEntry?[] _entries = new ServerLogEntry?[
        capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))
    ];
    private readonly Lock _lock = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private long _written;
    private ServerLogEntry? _lastError;

    public int Capacity => _entries.Length;

    public ServerLogEntry? LastError
    {
        get
        {
            lock (_lock)
                return _lastError is { } error && error.Sequence > _written - _entries.Length
                    ? error
                    : null;
        }
    }

    public IReadOnlyList<ServerLogEntry> Recent(
        int take,
        LogLevel minimum = LogLevel.Information,
        string? contains = null
    )
    {
        var found = new List<ServerLogEntry>();

        if (take <= 0)
            return found;

        lock (_lock)
        {
            var oldest = Math.Max(0, _written - _entries.Length);

            for (var n = _written - 1; n >= oldest && found.Count < take; n--)
            {
                var entry = _entries[n % _entries.Length]!;

                if (entry.Level >= minimum && Holds(entry, contains))
                    found.Add(entry);
            }
        }

        return found;
    }

    public ILogger CreateLogger(string categoryName) => new Writer(this, categoryName);

    public void Dispose() { }

    private void Add(LogLevel level, string category, string message, Exception? exception)
    {
        var text = exception?.ToString();

        if (text is { Length: > MAX_EXCEPTION_CHARS })
            text = text[..MAX_EXCEPTION_CHARS] + " ...";

        lock (_lock)
        {
            var entry = new ServerLogEntry(
                _written + 1,
                _clock.GetUtcNow().UtcDateTime,
                level,
                category,
                message,
                text
            );

            _entries[_written % _entries.Length] = entry;
            _written++;

            if (level >= LogLevel.Error)
                _lastError = entry;
        }
    }

    private static bool Holds(ServerLogEntry entry, string? text) =>
        string.IsNullOrEmpty(text)
        || entry.Category.Contains(text, StringComparison.OrdinalIgnoreCase)
        || entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase)
        || (entry.Exception?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);

    private sealed class Writer(ServerLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
                return;

            log.Add(logLevel, category, formatter(state, exception), exception);
        }
    }
}
