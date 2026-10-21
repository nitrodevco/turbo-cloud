using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Turbo.Catalog.Editing;

/// <summary>
/// How a catalog backup keeps its rows: gzipped JSON, each journaled table under its name in the
/// database and each row's columns under their property names. Read back through the model, so
/// every value comes back as its property's type; a column the backup predates comes back as a
/// new row would have it.
/// </summary>
internal static class CatalogBackupFormat
{
    private const int VERSION = 1;
    private const string VERSION_KEY = "version";
    private const string TABLES_KEY = "tables";

    public static byte[] Write(IModel model, IReadOnlyDictionary<CatalogRow, CatalogRowImage> rows)
    {
        var tables = CatalogEditJournal.Tables.ToDictionary(
            x => model.FindEntityType(x)!.GetTableName()!,
            x =>
                rows.Where(row => row.Key.Type == x)
                    .OrderBy(row => row.Key.Id)
                    .Select(row => row.Value.Values)
                    .ToList()
        );
        using var buffer = new MemoryStream();

        using (var zip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(
                zip,
                new Dictionary<string, object> { [VERSION_KEY] = VERSION, [TABLES_KEY] = tables }
            );
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The rows a backup holds; null when it was written by a version of this format that is
    /// not known here. Throws <see cref="InvalidDataException"/> or <see cref="JsonException"/>
    /// when it is not a backup at all.
    /// </summary>
    public static CatalogBackupContent? Read(IModel model, byte[] data)
    {
        using var zip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var document = JsonDocument.Parse(zip);
        var root = document.RootElement;

        if (
            !root.TryGetProperty(VERSION_KEY, out var version)
            || version.GetInt32() != VERSION
            || !root.TryGetProperty(TABLES_KEY, out var tables)
        )
            return null;

        var rows = new Dictionary<CatalogRow, CatalogRowImage>();
        var held = new HashSet<Type>();

        foreach (var type in CatalogEditJournal.Tables)
        {
            var entityType = model.FindEntityType(type)!;

            if (!tables.TryGetProperty(entityType.GetTableName()!, out var table))
                continue;

            held.Add(type);

            var fresh = Activator.CreateInstance(type)!;

            foreach (var element in table.EnumerateArray())
            {
                var values = new Dictionary<string, object?>();

                foreach (var property in entityType.GetProperties())
                {
                    values[property.Name] = element.TryGetProperty(property.Name, out var value)
                        ? value.Deserialize(property.ClrType)
                        : property.PropertyInfo?.GetValue(fresh);
                }

                var image = new CatalogRowImage(values);

                rows[new CatalogRow(type, image.Id)] = image;
            }
        }

        return new CatalogBackupContent(rows, held);
    }
}

/// <summary>
/// A backup's rows, and the tables it holds: one taken before a table was journaled has none of
/// it, which says nothing of what that table held then.
/// </summary>
internal sealed record CatalogBackupContent(
    Dictionary<CatalogRow, CatalogRowImage> Rows,
    IReadOnlySet<Type> Tables
);
