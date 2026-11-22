using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Turbo.Database.Backups;
using Turbo.Database.Context;

namespace Turbo.Import.Target;

/// <summary>
/// Writes entities as multi-row INSERTs, many times faster than change tracking for the millions
/// of rows a hotel has. The columns, their names and how each value is stored (an enum as its
/// number, a time as UTC, a list as JSON) come from the EF model, so a row is written exactly as
/// <c>SaveChanges</c> would write it.
/// <para>
/// A property the database fills in on insert (an id, <c>created_at</c>) is written when every
/// row of the call sets it and left to the database when none does; a computed one
/// (<c>updated_at</c>) is never written.
/// </para>
/// </summary>
internal sealed class TableWriter(TurboDbContext db, bool dryRun)
{
    private const int MAX_PARAMETERS = 16_000;
    private const int MAX_ROWS = 1_000;

    // Characters of text a statement, well inside MariaDB's default 16 MB max_allowed_packet even
    // when every character is four bytes and escaped.
    private const int MAX_BYTES = 1024 * 1024;

    private readonly Dictionary<Type, TableShape> _shapes = [];

    /// <summary>Writes the rows in one transaction; answers how many were written.</summary>
    public async Task<int> InsertAsync<T>(IReadOnlyList<T> rows, CancellationToken ct)
        where T : class
    {
        if (rows.Count == 0)
            return 0;

        var shape = Shape(typeof(T));
        var columns = shape.ColumnsFor(rows);

        if (dryRun)
            return rows.Count;

        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            await using var txScope = tx.ConfigureAwait(false);
            var conn = db.Database.GetDbConnection();
            var dbTx = tx.GetDbTransaction();

            var perStatement = Math.Max(1, Math.Min(MAX_ROWS, MAX_PARAMETERS / columns.Count));
            var start = 0;

            while (start < rows.Count)
            {
                var cmd = conn.CreateCommand();

                await using var cmdScope = cmd.ConfigureAwait(false);

                cmd.Transaction = dbTx;
                cmd.CommandTimeout = 0;

                var sql = new StringBuilder(shape.InsertPrefix(columns));
                var bytes = 0;
                var end = start;

                for (; end < rows.Count && end - start < perStatement && bytes < MAX_BYTES; end++)
                {
                    sql.Append(end == start ? "(" : ",(");

                    for (var c = 0; c < columns.Count; c++)
                    {
                        var name = $"@p{cmd.Parameters.Count}";
                        var value = columns[c].Read(rows[end]);
                        var parameter = cmd.CreateParameter();

                        parameter.ParameterName = name;
                        parameter.Value = value ?? DBNull.Value;
                        cmd.Parameters.Add(parameter);

                        if (value is string s)
                            bytes += s.Length;

                        if (c > 0)
                            sql.Append(',');

                        sql.Append(name);
                    }

                    sql.Append(')');
                }

                cmd.CommandText = sql.ToString();

                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                start = end;
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return rows.Count;
    }

    private TableShape Shape(Type type)
    {
        if (_shapes.TryGetValue(type, out var shape))
            return shape;

        var entity = db.Model.FindEntityType(type)
            ?? throw new InvalidOperationException($"{type.Name} is not an entity of the model.");

        _shapes[type] = shape = new TableShape(
            entity,
            DatabaseDump.IsSqlite(db),
            DatabaseDump.Quote(db, entity.GetTableName()!)
        );

        return shape;
    }

    private sealed class TableShape
    {
        private readonly string _table;
        private readonly bool _sqlite;
        private readonly List<Column> _always = [];
        private readonly List<Column> _generated = [];

        public TableShape(IEntityType entity, bool sqlite, string quotedTable)
        {
            _table = quotedTable;
            _sqlite = sqlite;

            var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());

            foreach (var property in entity.GetProperties())
            {
                var name = property.GetColumnName(store);

                if (name is null || property.PropertyInfo is null)
                    continue;

                // Computed by the database on every write (updated_at): never ours to write.
                if (
                    property.GetComputedColumnSql(store) is not null
                    || property.ValueGenerated.HasFlag(ValueGenerated.OnUpdate)
                )
                    continue;

                var column = new Column(name, property);

                if (property.ValueGenerated == ValueGenerated.OnAdd)
                    _generated.Add(column);
                else
                    _always.Add(column);
            }
        }

        public string InsertPrefix(IReadOnlyList<Column> columns) =>
            $"INSERT INTO {_table} ({string.Join(", ", columns.Select(c => Quote(c.Name)))}) VALUES ";

        /// <summary>
        /// The columns these rows are written with: every plain column, and each generated one
        /// the rows all set. A generated column some rows set and some leave is a mistake in the
        /// import, not something to guess about.
        /// </summary>
        public List<Column> ColumnsFor<T>(IReadOnlyList<T> rows)
            where T : class
        {
            var columns = new List<Column>(_always);

            foreach (var column in _generated)
            {
                var set = rows.Count(column.IsSet);

                if (set == rows.Count)
                    columns.Add(column);
                else if (set != 0)
                    throw new InvalidOperationException(
                        $"{typeof(T).Name}.{column.Property.Name} is set on {set} of {rows.Count} rows: set it on all or none."
                    );
            }

            return columns;
        }

        private string Quote(string name) =>
            _sqlite ? $"\"{name}\"" : $"`{name.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private sealed class Column(string name, IProperty property)
    {
        private readonly Func<object?, object?>? _toProvider = (
            property.GetValueConverter() ?? property.GetTypeMapping().Converter
        )?.ConvertToProvider;

        private readonly object? _clrDefault = property.ClrType.IsValueType
            ? Activator.CreateInstance(property.ClrType)
            : null;

        public string Name => name;

        public IProperty Property => property;

        public bool IsSet(object row) => !Equals(property.PropertyInfo!.GetValue(row), _clrDefault);

        public object? Read(object row)
        {
            var value = property.PropertyInfo!.GetValue(row);

            return value is null ? null
                : _toProvider is null ? value
                : _toProvider(value);
        }
    }
}
