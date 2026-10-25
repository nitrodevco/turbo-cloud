using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Plugins;

namespace Turbo.Database.Migrations;

/// <summary>
/// A plugin context's migrations, run as the plugin loads, and its tables, dropped by an
/// uninstall that drops its data: what every plugin's own module did.
/// </summary>
public sealed class PluginDbModule<TContext> : IPluginDbModule
    where TContext : DbContext
{
    public Task MigrateAsync(IServiceProvider sp, CancellationToken ct) =>
        MigrationHelper.MigrateAsync<TContext>(sp, ct);

    public Task UninstallAsync(IServiceProvider sp, CancellationToken ct) =>
        MigrationHelper.UninstallAsync<TContext>(sp, ct);
}
