using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Streams;
using Scrutor;
using Turbo.Authorization;
using Turbo.Authorization.Players.Contexts;
using Turbo.Authorization.Players.Requirements;
using Turbo.Core.Authorization;
using Turbo.Core.Configuration;
using Turbo.Core.Game.Players;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Session;
using Turbo.Database.Context;
using Turbo.Main.Configuration;
using Turbo.Main.Extensions;
using Turbo.Networking;
using Turbo.Networking.Factories;
using Turbo.Networking.Session;
using Turbo.Players;
using Turbo.Streams;

namespace Turbo.Main;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            })
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

                services.AddSingleton<INetworkEventLoopGroup, NetworkEventLoopGroup>();
                services.AddSingleton<INetworkServerFactory, NetworkServerFactory>();
                services.AddSingleton<INetworkManager, NetworkManager>();

                services.AddSingleton<ISessionFactory, SessionFactory>();
                services.AddSingleton<ISessionManager, SessionManager>();

                // Emulator
                services.AddSingleton<TurboEmulator>();

                services.AddSingleton<IAuthorizationManager, AuthorizationManager>();

                services.AddTransient<IRequirementHandler<NotBannedRequirement, PlayerLoginContext>, NotBannedHandler>();
                services.AddSingleton<IPlayerManager, PlayerManager>();
            })
            .UseOrleans(silo =>
            {
                silo.ConfigureEndpoints("127.0.0.1", siloPort: 11111, gatewayPort: 3000, listenOnAnyHostAddress: true);

                silo.UseLocalhostClustering()
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole();
                    })
                    .AddMemoryGrainStorage("PubSubStore")
                    .AddMemoryGrainStorage("PlayerStore");

                var streamTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .Select(t => new { Type = t, Attr = t.GetCustomAttribute<AutoStreamProviderAttribute>() })
                    .Where(x => x.Attr is not null);

                foreach (var x in streamTypes)
                {
                    var name = x.Attr!.Name;
                    var queueCount = x.Attr!.QueueCount;
                    var streamPubSubType = x.Attr!.StreamPubSubType;

                    silo.AddMemoryStreams(name, opts =>
                    {
                        if (queueCount is int n && n > 0) opts.ConfigurePartitioning(n);

                        opts.ConfigureStreamPubSub(streamPubSubType);
                    });
                }
            });

        var host = builder.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var shutdownToken = lifetime.ApplicationStopping;

        await host.StartAsync(shutdownToken);

        var emulator = host.Services.GetRequiredService<TurboEmulator>();

        await emulator.StartAsync(shutdownToken);

        await host.WaitForShutdownAsync(shutdownToken);
    }
}