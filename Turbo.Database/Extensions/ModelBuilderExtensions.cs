using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Turbo.Database.Attributes;

namespace Turbo.Database.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyDefaultAttributesFromEntities(this ModelBuilder modelBuilder)
    {
        // Iterate only types already in the model (no assembly hardcoding)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            var entity = modelBuilder.Entity(clr);

            foreach (var prop in clr.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                // Skip if property not mapped
                var propMeta = entityType.FindProperty(prop.Name);
                if (propMeta is null)
                    continue;

                // 1) SQL default
                var sqlAttr = prop.GetCustomAttribute<DefaultValueSqlAttribute>();
                if (sqlAttr is not null)
                {
                    entity.Property(prop.Name).HasDefaultValueSql(sqlAttr.Sql);
                    continue; // if you set SQL default, you typically skip constant default
                }

                // 2) Constant default (supports enums)
                var constAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
                if (constAttr is not null)
                {
                    // If this is an enum, HasDefaultValue(enumValue) is fine
                    // provided the property is mapped as enum (or has a converter).
                    entity.Property(prop.Name).HasDefaultValue(constAttr.Value);
                }

                // 3) Optional: enum storage guidance (int/long/string)
                var enumAttr = prop.GetCustomAttribute<EnumStorageAttribute>();
                if (enumAttr is not null && prop.PropertyType.IsEnum)
                {
                    var underlying = enumAttr.Underlying ?? typeof(int);
                    if (underlying == typeof(int))
                        entity.Property(prop.Name).HasConversion<int>();
                    else if (underlying == typeof(long))
                        entity.Property(prop.Name).HasConversion<long>();
                    else if (underlying == typeof(string))
                        entity.Property(prop.Name).HasConversion<string>();
                }
            }
        }
    }

    public static void ApplyConventions(this ModelBuilder mb)
    {
        foreach (
            var p in mb
                .Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(string))
        )
            p.SetMaxLength(p.GetMaxLength() ?? 512);

        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        );

        foreach (
            var p in mb
                .Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(DateTime))
        )
            p.SetValueConverter(utc);

        foreach (
            var p in mb
                .Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal))
        )
        {
            p.SetPrecision(p.GetPrecision() ?? 18);
            p.SetScale(p.GetScale() ?? 6);
        }
    }

    public static void ApplyTablePrefix(this ModelBuilder mb, string prefix, string? schema = null)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return;

        foreach (var entity in mb.Model.GetEntityTypes())
        {
            // Skip owned types (mapped into owner's table)
            if (entity.IsOwned())
                continue;

            var current = entity.GetTableName();

            if (string.IsNullOrEmpty(current))
                continue;

            // Don’t double-prefix
            if (!current.StartsWith(prefix, StringComparison.Ordinal))
            {
                entity.SetTableName(prefix + current);
            }

            if (!string.IsNullOrEmpty(schema))
            {
                entity.SetSchema(schema);
            }
        }
    }
}
