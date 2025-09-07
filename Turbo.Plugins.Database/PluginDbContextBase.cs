using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins.Database;

public abstract class PluginDbContextBase(DbContextOptions options, PluginRuntimeConfig runtime)
    : DbContext(options)
{
    protected readonly PluginRuntimeConfig Runtime = runtime;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        ApplyTablePrefix(mb, Runtime.TablePrefix);
        ApplyConventions(mb);

        mb.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private static void ApplyTablePrefix(ModelBuilder mb, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return;

        foreach (var et in mb.Model.GetEntityTypes())
        {
            // Skip shadow/shared types (e.g., query types) that don't map to a table
            if (et.GetTableName() is not string current || string.IsNullOrWhiteSpace(current))
                continue;

            // Don’t double-prefix if the plugin explicitly named it with the prefix already
            if (!current.StartsWith(prefix, StringComparison.Ordinal))
                et.SetTableName(prefix + current);
        }
    }

    private static void ApplyConventions(ModelBuilder mb)
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
}
