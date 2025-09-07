using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboDatabase(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<DatabaseConfig>().Bind(cfg.GetSection(DatabaseConfig.SECTION_NAME));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseConfig>>().Value);

        services.AddDbContextFactory<TurboDbContext>(
            (sp, options) =>
            {
                var dbConfig = sp.GetRequiredService<DatabaseConfig>();
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
}
