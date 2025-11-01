using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Authentication;
using Turbo.Catalog;
using Turbo.Crypto.Extensions;
using Turbo.Database.Extensions;
using Turbo.Events.Extensions;
using Turbo.Furniture;
using Turbo.Grains.Extensions;
using Turbo.Inventory;
using Turbo.Logging.Extensions;
using Turbo.Main.Console;
using Turbo.Messages.Extensions;
using Turbo.Navigator;
using Turbo.Networking.Extensions;
using Turbo.PacketHandlers;
using Turbo.Players;
using Turbo.Plugins.Extensions;
using Turbo.Rooms;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Main;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var bootstrapLogger = LoggerFactory
            .Create(builder =>
            {
                builder.ClearProviders();
                builder.AddTurboConsoleLogger();
            })
            .CreateLogger("Bootstrap");

        System.Console.WriteLine(
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

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddEnvironmentVariables(prefix: "TURBO__");

        if (builder.Environment.IsDevelopment())
        {
            bootstrapLogger.LogInformation("=== Configuration Providers ===");
            foreach (var p in ((IConfigurationRoot)builder.Configuration).Providers)
            {
                if (p is JsonConfigurationProvider jp)
                {
                    var src = (JsonConfigurationSource)jp.Source;
                    var path = src.Path;

                    if (path is not null)
                    {
                        var fileProvider =
                            src.FileProvider ?? builder.Environment.ContentRootFileProvider;
                        var fi = fileProvider?.GetFileInfo(path);
                        var physical = fi?.PhysicalPath ?? "<virtual or unresolved>";

                        bootstrapLogger.LogInformation($"Json: '{path}' -> {physical}");
                    }
                }
            }
            bootstrapLogger.LogInformation("===============================");
        }

        builder.AddTurboGrains();

        builder.Services.AddTurboLogging(builder);
        builder.Services.AddTurboNetworking(builder);
        builder.Services.AddTurboPlugins(builder);
        builder.Services.AddTurboDatabaseContext(builder);
        builder.Services.AddTurboEventSystem();
        builder.Services.AddTurboMessageSystem();
        builder.Services.AddTurboCrypto(builder);

        builder.Services.AddHostPlugin<AuthenticationModule>();
        builder.Services.AddHostPlugin<FurnitureModule>();
        builder.Services.AddHostPlugin<CatalogModule>();
        builder.Services.AddHostPlugin<PlayerModule>();
        builder.Services.AddHostPlugin<InventoryModule>();
        builder.Services.AddHostPlugin<NavigatorModule>();
        builder.Services.AddHostPlugin<RoomModule>();
        builder.Services.AddHostPlugin<PacketHandlersModule>();

        builder.Services.AddSingleton<AssemblyProcessor>();
        builder.Services.AddSingleton<ConsoleCommandService>();

        builder.Services.AddHostedService<TurboEmulator>();

        var host = builder.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var ct = lifetime.ApplicationStopping;

        try
        {
            await host.StartAsync(ct).ConfigureAwait(false);

            bootstrapLogger.LogInformation(
                "Started {GetProjectName} {GetProductVersion}",
                GetProjectName(),
                GetProjectVersion()
            );

            host.Services.GetService<ConsoleCommandService>()?.Enable();

            await host.WaitForShutdownAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            bootstrapLogger.LogCritical(ex, "Host terminated unexpectedly");
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
