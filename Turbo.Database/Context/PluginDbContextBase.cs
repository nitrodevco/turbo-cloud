using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Delegates;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context;

/// <summary>
/// A plugin's tables: under its manifest's table prefix, with its own migrations history, never
/// cascading into the hotel's tables. A context with no constructor of its own needs nothing else:
/// the host finds it in the plugin's assembly, registers it and migrates its tables as the plugin
/// loads, and <c>dotnet ef migrations add</c> builds it by itself (design time reads the prefix
/// from the plugin's <c>manifest.json</c>, see <see cref="PluginDesignTime"/>).
/// </summary>
public class PluginDbContextBase<TContent> : DbContextBase<TContent>
    where TContent : DbContext
{
    private TablePrefixProvider? _prefix;
    private Action<DbContextOptionsBuilder>? _configure;

    /// <summary>A context registered by hand (<c>AddPluginDatabaseContext</c>).</summary>
    public PluginDbContextBase(DbContextOptions<TContent> options, TablePrefixProvider prefix)
        : base(options) => _prefix = prefix;

    /// <summary>
    /// A context by convention: the host gives it its connection and prefix
    /// (<see cref="ServiceCollectionExtensions.AddPluginDatabase{TContext}"/>), and with none
    /// given it is being built at design time.
    /// </summary>
    protected PluginDbContextBase() { }

    /// <summary>Set by the host before the context is used.</summary>
    internal void UseHost(TablePrefixProvider prefix, Action<DbContextOptionsBuilder> configure)
    {
        _prefix = prefix;
        _configure = configure;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (optionsBuilder.IsConfigured)
            return;

        if (_configure is not null)
        {
            _configure(optionsBuilder);

            return;
        }

        var design = PluginDesignTime.For(GetType());

        _prefix ??= () => design.TablePrefix;
        design.Configure(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        mb.ApplyTablePrefix(_prefix?.Invoke() ?? string.Empty);
    }
}
