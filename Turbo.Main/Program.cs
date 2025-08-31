using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using Turbo.Core.Configuration;
using Turbo.Core.Game.Players;
using Turbo.Database.Context;
using Turbo.Main.Configuration;
using Turbo.Main.Extensions;
using Turbo.Players;
using Turbo.Streams;

namespace Turbo.Main;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var bootstrapLogger = LoggerFactory
            .Create(builder =>
            {
                builder.AddSimpleConsole(op =>
                {
                    op.IncludeScopes = true;
                    op.SingleLine = true;
                    op.TimestampFormat = "HH:mm:ss ";
                });
            })
            .CreateLogger("Bootstrap");

        Console.WriteLine(
            @"
                ████████╗██╗   ██╗██████╗ ██████╗  ██████╗ 
                ╚══██╔══╝██║   ██║██╔══██╗██╔══██╗██╔═══██╗
                   ██║   ██║   ██║██████╔╝██████╔╝██║   ██║
                   ██║   ██║   ██║██╔══██╗██╔══██╗██║   ██║
                   ██║   ╚██████╔╝██║  ██║██████╔╝╚██████╔╝
                   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═════╝  ╚═════╝ 
            "
        );

        bootstrapLogger.LogInformation(
            "Starting {GetProjectName} {GetProductVersion}",
            GetProjectName(),
            GetProjectVersion()
        );

        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureLogging(
            (ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                logging.AddConsole();
            }
        );

        builder.ConfigureServices(
            (ctx, services) =>
            {
                var turboConfig = new TurboConfig();
                ctx.Configuration.Bind(TurboConfig.Turbo, turboConfig);
                services.AddSingleton<IEmulatorConfig>(turboConfig);

                services.AddNetworking();

                services.AddSingleton<IPlayerManager, PlayerManager>();

                // Emulator
                services.AddHostedService<TurboEmulator>();
            }
        );

        builder.UseOrleans(silo =>
        {
            silo.ConfigureEndpoints(
                "127.0.0.1",
                siloPort: 11111,
                gatewayPort: 3000,
                listenOnAnyHostAddress: true
            );

            silo.UseLocalhostClustering()
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorage("PlayerStore");

            var streamTypes = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Type = t,
                    Attr = t.GetCustomAttribute<AutoStreamProviderAttribute>(),
                })
                .Where(x => x.Attr is not null);

            foreach (var x in streamTypes)
            {
                var name = x.Attr!.Name;
                var queueCount = x.Attr!.QueueCount;
                var streamPubSubType = x.Attr!.StreamPubSubType;

                silo.AddMemoryStreams(
                    name,
                    opts =>
                    {
                        if (queueCount is int n && n > 0)
                        {
                            opts.ConfigurePartitioning(n);
                        }

                        opts.ConfigureStreamPubSub(streamPubSubType);
                    }
                );
            }
        });

        builder.UseTurboPlugins(
            Path.Combine(AppContext.BaseDirectory, "plugins"),
            [@"^Turbo\.", @"^Microsoft\.Extensions\.", @"^System(\..+)?$"],
            [],
            bootstrapLogger
        );

        builder.ConfigureServices(
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

        var host = builder.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var shutdownToken = lifetime.ApplicationStopping;

        try
        {
            await host.StartAsync(shutdownToken);

            bootstrapLogger.LogInformation(
                "Started {GetProjectName} {GetProductVersion}",
                GetProjectName(),
                GetProjectVersion()
            );

            await host.WaitForShutdownAsync(shutdownToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private static string GetProjectName()
    {
        return "Turbo Emulator";
    }

    public static Version GetProjectVersion()
    {
        return new Version(
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0"
        );
    }
}
