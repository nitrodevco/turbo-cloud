using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using SuperSocket;
using SuperSocket.Connection;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using Turbo.Core.Configuration;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Revisions;
using Turbo.Database.Context;
using Turbo.Main.Configuration;
using Turbo.Main.Extensions;
using Turbo.Networking;
using Turbo.Networking.Session;
using Turbo.Packets.Revisions;
using Turbo.Players;
using Turbo.Streams;

namespace Turbo.Main;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.ConfigureLogging(
            (ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                logging.AddSimpleConsole();
            }
        );

        builder.ConfigureServices(
            (ctx, services) =>
            {
                var turboConfig = new TurboConfig();
                ctx.Configuration.Bind(TurboConfig.Turbo, turboConfig);
                services.AddSingleton<IEmulatorConfig>(turboConfig);

                var connectionString = ctx.Configuration.GetConnectionString("DefaultConnection");

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
                        .EnableSensitiveDataLogging(turboConfig.DatabaseLoggingEnabled)
                        .EnableDetailedErrors()
                );

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

        var host = builder.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var shutdownToken = lifetime.ApplicationStopping;

        try
        {
            await host.StartAsync(shutdownToken);
            await host.WaitForShutdownAsync(shutdownToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
