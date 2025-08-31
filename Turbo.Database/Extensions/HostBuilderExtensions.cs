using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Database.Context;

namespace Turbo.Database.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder UseTurboDatabase(this IHostBuilder host)
    {
        host.ConfigureServices(
            (ctx, services) =>
            {
                var dbConfig = ctx.Configuration.GetSection("Turbo:Database");
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
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(CoreEventId.RedundantIndexRemoved)
                        )
                        .EnableSensitiveDataLogging(dataLoggingEnabled)
                        .EnableDetailedErrors()
                );
            }
        );

        return host;
    }
}
