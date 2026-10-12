using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Turbo.Database.Configuration;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context
{
    public class TurboDbContextFactory : IDesignTimeDbContextFactory<TurboDbContext>
    {
        public TurboDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TurboDbContext>();

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            ServiceCollectionExtensions.UseTurboPostgres(
                optionsBuilder,
                new DatabaseConfig
                {
                    ConnectionString =
                        configuration["Turbo:Database:ConnectionString"] ?? string.Empty,
                    LoggingEnabled = true,
                },
                npgsql => npgsql.MigrationsAssembly("Turbo.Database")
            );

            return new TurboDbContext(optionsBuilder.Options);
        }
    }
}
