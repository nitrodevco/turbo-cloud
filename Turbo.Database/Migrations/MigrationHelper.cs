using System;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Database.Delegates;

namespace Turbo.Database.Migrations;

public static class MigrationHelper
{
    /// <summary>
    /// Migrates a plugin's tables as the plugin loads, the way the host's configuration says
    /// (<c>Turbo:Database:Migrate</c>): the same lock, the same refusals and the same report as
    /// the emulator's own tables. Asked again when the plugin hot reloads, which has nothing to
    /// say unless a migration is new.
    /// </summary>
    public static async Task MigrateAsync<TContext>(IServiceProvider sp, CancellationToken ct)
        where TContext : DbContext
    {
        using var db = sp.GetRequiredService<TContext>();

        var migrator = sp.GetRequiredService<IHostServices>()
            .GetRequiredService<DatabaseMigrator>();
        var run = new MigrationRunOptions { QuietWhenCurrent = true };
        var alc = AssemblyLoadContext.GetLoadContext(db.GetType().Assembly);

        if (alc is not null)
        {
            using (alc.EnterContextualReflection())
                await migrator
                    .MigrateAsync(db, typeof(TContext).Name, run, ct)
                    .ConfigureAwait(false);
        }
        else
        {
            await migrator.MigrateAsync(db, typeof(TContext).Name, run, ct).ConfigureAwait(false);
        }
    }

    public static async Task UninstallAsync<TContext>(IServiceProvider sp, CancellationToken ct)
        where TContext : DbContext
    {
        using var db = sp.GetRequiredService<TContext>();

        var prefix = sp.GetRequiredService<TablePrefixProvider>()();

        // The drop below matches every table that starts with the prefix. With no prefix that is
        // every table in the database, the hotel's included; anything but a plain identifier
        // would have to be escaped for SQL and for LIKE, so it is refused rather than escaped.
        if (
            string.IsNullOrEmpty(prefix)
            || !prefix.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
        )
            throw new InvalidOperationException(
                $"Refusing to uninstall {typeof(TContext).Name}: table prefix '{prefix}' is empty or not a plain identifier, so its tables cannot be told apart from the hotel's."
            );

        // `_` is a LIKE wildcard: "tsp_" would otherwise also match "tspx...".
        var tablePrefix = prefix.Replace("_", @"\_");

        var sql =
            $@"
SET @sql = (
  SELECT GROUP_CONCAT(CONCAT('DROP TABLE IF EXISTS `', TABLE_SCHEMA, '`.`', TABLE_NAME, '`') SEPARATOR ';')
  FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE '{tablePrefix}%'
);
SET FOREIGN_KEY_CHECKS = 0;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET FOREIGN_KEY_CHECKS = 1;";
        await db.Database.ExecuteSqlRawAsync(sql, ct).ConfigureAwait(false);
    }
}
