using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Delegates;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboDatabaseContext(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<DatabaseConfig>(
            builder.Configuration.GetSection(DatabaseConfig.SECTION_NAME)
        );

        services.AddDbContextFactory<TurboDbContext>(
            (sp, options) =>
            {
                var dbConfig = sp.GetRequiredService<IOptions<DatabaseConfig>>().Value;
                var connectionString = dbConfig.ConnectionString;
                var loggingEnabled = dbConfig.LoggingEnabled;

                options
                    .UseMySql(
                        connectionString,
                        ServerVersion.AutoDetect(connectionString),
                        options =>
                        {
                            options.MigrationsAssembly("Turbo.Main");
                        }
                    )
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(CoreEventId.RedundantIndexRemoved)
                    )
                    .EnableSensitiveDataLogging(loggingEnabled)
                    .EnableDetailedErrors();
            }
        );

        return services;
    }

    public static IServiceCollection AddPluginDatabaseContext<TContext>(
        this IServiceCollection services
    )
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(
            (sp, options) =>
            {
                var prefix = sp.GetRequiredService<TablePrefixProvider>();
                var dbConfig = sp.GetRequiredService<IOptions<DatabaseConfig>>().Value;
                var connectionString = dbConfig.ConnectionString;
                var loggingEnabled = dbConfig.LoggingEnabled;

                var asm = typeof(TContext).Assembly.FullName;

                options
                    .UseMySql(
                        connectionString,
                        ServerVersion.AutoDetect(connectionString),
                        builder =>
                        {
                            builder.MigrationsHistoryTable(
                                $"__EFMigrationsHistory_{prefix().TrimEnd('_')}"
                            );
                        }
                    )
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(CoreEventId.RedundantIndexRemoved)
                    )
                    .EnableSensitiveDataLogging(loggingEnabled)
                    .EnableDetailedErrors();
            }
        );

        return services;
    }
}
