using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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

            var connectionString = configuration["Turbo:Database:ConnectionString"];

            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                options =>
                {
                    options.MigrationsAssembly("Turbo.Database");
                }
            );

            return new TurboDbContext(optionsBuilder.Options);
        }
    }
}
