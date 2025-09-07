using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddPluginDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(
            (sp, opts) =>
            {
                var rc = sp.GetRequiredService<PluginRuntimeConfig>();
                var asm = typeof(TContext).Assembly.FullName;

                opts.UseMySql(
                    rc.ConnectionString,
                    ServerVersion.AutoDetect(rc.ConnectionString),
                    builder =>
                    {
                        builder.MigrationsAssembly(asm);
                        builder.MigrationsHistoryTable(
                            $"__EFMigrationsHistory_{rc.TablePrefix.TrimEnd('_')}"
                        );
                    }
                );
            }
        );
    }
}
