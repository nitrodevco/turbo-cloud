using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Database.Context;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboDatabase(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        var dbConfig = cfg.GetSection("Turbo:Database");
        var connectionString = dbConfig.GetConnectionString("DefaultConnection");
        var dataLoggingEnabled = dbConfig.GetValue<bool>("DatabaseLoggingEnabled");

        services.AddDbContextFactory<TurboDbContext>(options =>
            options
                .UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    options =>
                    {
                        options.MigrationsAssembly("Turbo.Main");
                    }
                )
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.RedundantIndexRemoved))
                .EnableSensitiveDataLogging(dataLoggingEnabled)
                .EnableDetailedErrors()
        );

        return services;
    }
}
