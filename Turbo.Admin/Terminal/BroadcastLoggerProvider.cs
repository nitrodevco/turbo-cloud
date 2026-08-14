using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Turbo.Admin.Terminal;

internal sealed class BroadcastLoggerProvider(IConsoleBroadcaster broadcaster) : ILoggerProvider
{
    private readonly IConsoleBroadcaster _broadcaster = broadcaster;
    private readonly ConcurrentDictionary<string, BroadcastLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new BroadcastLogger(name, _broadcaster));

    public void Dispose() => _loggers.Clear();
}

internal sealed class BroadcastLogger(string categoryName, IConsoleBroadcaster broadcaster)
    : ILogger
{
    private readonly string _shortCategory = categoryName.Split('.')[^1];
    private readonly IConsoleBroadcaster _broadcaster = broadcaster;

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

        var message = formatter(state, exception);
        var line =
            $"[{DateTime.Now:HH:mm:ss.fff}] {LevelTag(logLevel)} {_shortCategory}: {message}";

        if (exception is not null)
            line += Environment.NewLine + exception;

        _broadcaster.Publish(line);
    }

    private static string LevelTag(LogLevel level) =>
        level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none",
        };
}
