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

    public SqliteDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<TurboDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new StampTimestamps())
            .ConfigureWarnings(w =>
            {
                w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
                w.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            })
            .Options;
        using var ctx = new SqliteTurboDbContext(_options);
        ctx.Database.EnsureCreated();
    }

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
                if (!typeof(TurboEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                entityType.FindProperty(nameof(TurboEntity.CreatedAt))!.ValueGenerated =
                    ValueGenerated.Never;
                entityType.FindProperty(nameof(TurboEntity.UpdatedAt))!.ValueGenerated =
                    ValueGenerated.Never;
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

                if (entry.Entity.UpdatedAt == default)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
