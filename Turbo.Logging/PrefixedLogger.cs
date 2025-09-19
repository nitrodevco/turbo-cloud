using System;
using Microsoft.Extensions.Logging;
using Turbo.Logging.Factories;

namespace Turbo.Logging;

public sealed class PrefixedLogger<T> : ILogger<T>
{
    private readonly ILogger _inner;

    public PrefixedLogger(IPrefixedLoggerFactory factory)
    {
        var categoryName = typeof(T).FullName ?? typeof(T).Name;

        _inner = factory.CreateLogger(categoryName);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _inner.Log(logLevel, eventId, state, exception, formatter);
}
