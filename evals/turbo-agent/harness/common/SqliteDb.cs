using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Turbo.Database.Context;

namespace EvalHarness;

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
            .ConfigureWarnings(w =>
            {
                w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
                w.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            })
            .Options;
        using var ctx = new TurboDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public TurboDbContext CreateDbContext() => new(_options);

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
                value = p.ClrType == typeof(string) ? "" : Activator.CreateInstance(Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType);
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
}
