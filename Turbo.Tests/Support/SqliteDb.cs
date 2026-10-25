using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Context;
using Turbo.Database.Entities;

namespace Turbo.Tests.Support;

/// <summary>
/// A relational (SQLite, in memory) TurboDbContext factory, for behaviour only a relational
/// provider shows: EF Core's warnings about row limits without ORDER BY are promoted to errors.
/// </summary>
public sealed class SqliteDb : IDbContextFactory<TurboDbContext>, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<TurboDbContext> _options;
    private readonly BeforeTransaction _beforeTransaction = new();
    private readonly CountFailedSaves _failedSaves = new();

    /// <summary>
    /// The saves that failed (a unique key taken, a constraint broken): each is one error EF Core
    /// logs on a server, so an expected path should have none.
    /// </summary>
    public int FailedSaves => _failedSaves.Count;

    /// <summary>
    /// Runs once, just before the next transaction begins, and is then cleared: what another
    /// activation commits between a caller's reads and its transaction, a race to interleave.
    /// </summary>
    public Func<Task>? BeforeNextTransaction
    {
        get => _beforeTransaction.Next;
        set => _beforeTransaction.Next = value;
    }

    public SqliteDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = OptionsFor(
            _conn,
            new StampTimestamps(),
            new NoRowLocks(),
            _beforeTransaction,
            _failedSaves
        );
        lock (SCHEMA)
            SCHEMA.Value.BackupDatabase(_conn);
    }

    /// <summary>
    /// The empty schema, created once a run and copied into each new database: EnsureCreated on
    /// the full schema took 200-300 ms a database, the copy takes a few.
    /// </summary>
    private static readonly Lazy<SqliteConnection> SCHEMA = new(() =>
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var ctx = new SqliteTurboDbContext(OptionsFor(conn));
        ctx.Database.EnsureCreated();

        return conn;
    });

    // The same warnings for the schema's context as for a database's, so EF builds one model.
    private static DbContextOptions<TurboDbContext> OptionsFor(
        SqliteConnection conn,
        params IInterceptor[] interceptors
    ) =>
        new DbContextOptionsBuilder<TurboDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(interceptors)
            .ConfigureWarnings(w =>
            {
                w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
                w.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            })
            .Options;

    public TurboDbContext CreateDbContext() => new SqliteTurboDbContext(_options);

    public Task<TurboDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(CreateDbContext());

    /// <summary>
    /// Inserts an entity row with every mapped column, database-generated ones included (SQLite
    /// has no generator for them). Navigation properties are ignored; set foreign keys.
    /// </summary>
    public void Insert(object entity)
    {
        using var ctx = CreateDbContext();
        var et = ctx.Model.FindEntityType(entity.GetType())!;
        var table = et.GetTableName()!;
        var cols = new List<string>();
        var ps = new List<SqliteParameter>();
        foreach (var p in et.GetProperties())
        {
            var col = p.GetColumnName();
            object? value = p.PropertyInfo?.GetValue(entity);
            if (value is DateTime dt && dt == default)
                value = DateTime.UtcNow;
            if (value is null && !p.IsNullable)
                value =
                    p.ClrType == typeof(string)
                        ? ""
                        : Activator.CreateInstance(
                            Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType
                        );
            var conv = p.GetTypeMapping().Converter;
            if (conv is not null && value is not null)
                value = conv.ConvertToProvider(value);
            if (value is not null && value.GetType().IsEnum)
                value = Convert.ToInt64(value);
            cols.Add(col);
            ps.Add(new SqliteParameter("$p" + ps.Count, value ?? DBNull.Value));
        }
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO \"{table}\" ({string.Join(", ", cols.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", ps.Select(x => x.ParameterName))})";
        cmd.Parameters.AddRange(ps);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    /// <summary>
    /// The model with the two timestamps written by the caller: MySQL generates them (and
    /// <c>updated_at</c> on every change), SQLite cannot, so here they are plain columns that
    /// <see cref="StampTimestamps"/> fills in.
    /// </summary>
    private sealed class SqliteTurboDbContext(DbContextOptions<TurboDbContext> options)
        : TurboDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            foreach (var entityType in mb.Model.GetEntityTypes())
            {
                // MySQL's collations mean nothing to SQLite, which compares case-sensitively anyway.
                foreach (var property in entityType.GetProperties())
                    property.SetCollation(null);

                if (!typeof(TurboEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                entityType.FindProperty(nameof(TurboEntity.CreatedAt))!.ValueGenerated =
                    ValueGenerated.Never;

                if (entityType.FindProperty("UpdatedAt") is { } updatedAt)
                    updatedAt.ValueGenerated = ValueGenerated.Never;
            }
        }
    }

    /// <summary>
    /// MySQL fills <c>created_at</c> and <c>updated_at</c> itself, and SQLite has no generator for
    /// them, so a row added through EF is stamped here instead, as the database would.
    /// </summary>
    private sealed class StampTimestamps : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result
        )
        {
            Stamp(eventData.Context);

            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken ct = default
        )
        {
            Stamp(eventData.Context);

            return ValueTask.FromResult(result);
        }

        private static void Stamp(DbContext? context)
        {
            foreach (
                var entry in context?.ChangeTracker.Entries<Turbo.Database.Entities.TurboEntity>()
                    ?? []
            )
            {
                if (entry.State != EntityState.Added)
                    continue;

                if (entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = DateTime.UtcNow;

                if (
                    entry.Metadata.FindProperty("UpdatedAt") is not null
                    && entry.Property("UpdatedAt").CurrentValue is DateTime updatedAt
                    && updatedAt == default
                )
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }

    private sealed class CountFailedSaves : SaveChangesInterceptor
    {
        private int _count;

        public int Count => _count;

        public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
            Interlocked.Increment(ref _count);

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken ct = default
        )
        {
            Interlocked.Increment(ref _count);

            return Task.CompletedTask;
        }
    }

    private sealed class BeforeTransaction : DbTransactionInterceptor
    {
        public Func<Task>? Next { get; set; }

        public override async ValueTask<
            InterceptionResult<System.Data.Common.DbTransaction>
        > TransactionStartingAsync(
            System.Data.Common.DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<System.Data.Common.DbTransaction> result,
            CancellationToken ct = default
        )
        {
            // Cleared first: what it runs begins transactions of its own.
            var next = Next;
            Next = null;
            if (next is not null)
                await next();

            return result;
        }
    }

    /// <summary>
    /// SQLite locks the whole database for a writer and has no <c>FOR UPDATE</c>; a query that
    /// takes a MySQL row lock runs here without it.
    /// </summary>
    private sealed class NoRowLocks : DbCommandInterceptor
    {
        public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result
        )
        {
            Strip(command);

            return result;
        }

        public override ValueTask<
            InterceptionResult<System.Data.Common.DbDataReader>
        > ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken ct = default
        )
        {
            Strip(command);

            return ValueTask.FromResult(result);
        }

        private static void Strip(System.Data.Common.DbCommand command) =>
            command.CommandText = command.CommandText.Replace(
                " FOR UPDATE",
                string.Empty,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
