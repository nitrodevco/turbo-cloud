using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Scrutor;
using Turbo.Core.Configuration;
using Turbo.Database.Context;
using Turbo.Main.Configuration;
using Turbo.Main.Extensions;
using Turbo.Main.Filters;

namespace Turbo.Main;

internal class Program
{
    public static void Main(string[] args)
    {
        try
        {
            CreateHostBuilder(args).Build().Run();
        }

        catch (Exception error)
        {
            Console.WriteLine(error);
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddUserSecrets<Program>();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging();

                // Configuration
                var turboConfig = new TurboConfig();
                hostContext.Configuration.Bind(TurboConfig.Turbo, turboConfig);
                services.AddSingleton<IEmulatorConfig>(turboConfig);

                var connectionString = hostContext.Configuration.GetConnectionString("DefaultConnection");

                Console.WriteLine($"Using connection string: {connectionString}");

                services.AddDbContextFactory<TurboDbContext>(options => options
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                        options => { options.MigrationsAssembly("Turbo.Main"); })
                    .ConfigureWarnings(warnings => warnings
                        .Ignore(CoreEventId.RedundantIndexRemoved))
                    .EnableSensitiveDataLogging(turboConfig.DatabaseLoggingEnabled)
                    .EnableDetailedErrors());

                // Emulator
                services.AddHostedService<TurboEmulator>();

                /* services.Scan(scan => scan
                    .FromApplicationDependencies(a => a.GetName().Name!.StartsWith("Turbo"))
                    .AddClasses(c => c.AssignableTo<IIncomingGrainCallFilter>())
                    .As<IIncomingGrainCallFilter>()
                    .WithSingletonLifetime());

                services.AddSingleton<CompositeIncomingFilter>(); */
            })
            .UseOrleans(silo =>
            {
                silo.UseLocalhostClustering()
                    .AddMemoryGrainStorageAsDefault()
                    .AddMemoryGrainStorage("PubSubStore")
                    .AddMemoryStreams("SMS")
                    .AddIncomingGrainCallFilter<AutoFlushFilter>();
            });
    }
}