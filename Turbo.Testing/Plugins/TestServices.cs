using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Events;

namespace Turbo.Testing;

/// <summary>
/// The hotel's settings (its configuration), which a plugin's settings classes are bound to: a
/// <c>[PluginSettings("Section")]</c> class reads <c>Section:Name</c>. A change reaches the
/// plugin's <c>IOptionsMonitor</c> at once, as a save in the admin panel does.
/// </summary>
public sealed class TestSettings
{
    private readonly IConfigurationRoot _configuration;

    internal TestSettings(TestHotel hotel, IConfigurationRoot configuration) =>
        _configuration = configuration;

    /// <summary>Sets <paramref name="key"/> (<c>Section:Name</c>, <c>Section:List:0</c>...) and tells the plugins.</summary>
    public TestSettings Set(string key, object? value)
    {
        _configuration[key] = value switch
        {
            null => null,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
        _configuration.Reload();

        return this;
    }

    /// <summary>The value of <paramref name="key"/>; null when it is not set.</summary>
    public string? Get(string key) => _configuration[key];
}

/// <summary>One line the hotel or a plugin logged.</summary>
public sealed record TestLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception
);

/// <summary>What the hotel and the plugins logged, in order.</summary>
public sealed class TestLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();

    public IReadOnlyList<TestLogEntry> Entries => [.. _entries];

    /// <summary>The lines at <paramref name="level"/> or above.</summary>
    public IReadOnlyList<TestLogEntry> AtLeast(LogLevel level) =>
        [.. _entries.Where(e => e.Level >= level)];

    ILogger ILoggerProvider.CreateLogger(string categoryName) => new Logger(this, categoryName);

    void IDisposable.Dispose() { }

    private sealed class Logger(TestLogs logs, string category) : ILogger
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
        ) =>
            logs._entries.Enqueue(
                new TestLogEntry(category, logLevel, formatter(state, exception), exception)
            );
    }
}

/// <summary>
/// The hotel's events: everything a plugin published (<see cref="Published"/>), and an event the
/// test publishes reaches every plugin's <c>IEventHandler&lt;T&gt;</c> of it, as the hotel's own
/// post-commit events do.
/// </summary>
public sealed class TestEvents : IEventPublisher
{
    private readonly TestHotel _hotel;
    private readonly ConcurrentQueue<IEvent> _published = new();
    private readonly ConcurrentQueue<Task> _committed = new();

    internal TestEvents(TestHotel hotel) => _hotel = hotel;

    /// <summary>Every event published through the hotel, the test's own included, in order.</summary>
    public IReadOnlyList<IEvent> Published => [.. _published];

    /// <summary>The events of type <typeparamref name="T"/> published, in order.</summary>
    public IReadOnlyList<T> Of<T>() => [.. _published.OfType<T>()];

    /// <summary>
    /// Publishes <paramref name="evt"/> to the plugins' handlers and waits for them, as the hotel
    /// publishes its events (a post-commit event too: the hotel does not wait for those, a test does).
    /// </summary>
    public async Task PublishAsync(IEvent evt, CancellationToken ct = default)
    {
        _published.Enqueue(evt);
        await _hotel.EventRegistry.PublishAsync(evt, null, ct).ConfigureAwait(false);
    }

    /// <summary>A plugin's post-commit event: its handlers run off the caller, as on a server (<see cref="SettledAsync"/> waits for them).</summary>
    public void PublishCommitted(DomainEvent evt)
    {
        _published.Enqueue(evt);
        _committed.Enqueue(
            Task.Run(() => _hotel.EventRegistry.PublishAsync(evt, null, CancellationToken.None))
        );
    }

    /// <summary>Waits until the handlers of every post-commit event published so far have run.</summary>
    public Task SettledAsync() => Task.WhenAll(_committed);
}

/// <summary>
/// The plugins' tables in an in-memory SQLite database (the real SQL: keys, constraints,
/// transactions). They are made from each context's model as it loads, under its table prefix.
/// </summary>
internal sealed class SqlitePluginDatabase(SqliteConnection connection) : PluginTestDatabase
{
    public override void Configure(DbContextOptionsBuilder options, string tablePrefix) =>
        options.UseSqlite(connection);

    public override async Task CreateAsync(DbContext db, CancellationToken ct)
    {
        // Made again on a reload, so each statement leaves what is there.
        var script = db
            .Database.GenerateCreateScript()
            .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace(
                "CREATE UNIQUE INDEX ",
                "CREATE UNIQUE INDEX IF NOT EXISTS ",
                StringComparison.Ordinal
            )
            .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal);

        foreach (
            var statement in script
                .Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
        )
        {
            await using var command = connection.CreateCommand();

            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
