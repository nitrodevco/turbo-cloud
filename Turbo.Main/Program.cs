using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Achievements;
using Turbo.Admin;
using Turbo.Authentication;
using Turbo.Catalog;
using Turbo.Commands;
using Turbo.Crypto.Extensions;
using Turbo.Database.Extensions;
using Turbo.Database.Migrations;
using Turbo.Events.Extensions;
using Turbo.Furniture;
using Turbo.Gamedata;
using Turbo.Guilds;
using Turbo.Inventory;
using Turbo.Logging.Extensions;
using Turbo.Main.Console;
using Turbo.Main.Extensions;
using Turbo.Main.Startup;
using Turbo.Messages.Extensions;
using Turbo.Navigator;
using Turbo.Networking.Extensions;
using Turbo.Operations;
using Turbo.PacketHandlers;
using Turbo.Players;
using Turbo.Plugins.Extensions;
using Turbo.Rooms;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Web;

namespace Turbo.Main;

internal class Program
{
    public static async Task<int> Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var bootstrapLogger = LoggerFactory
            .Create(builder =>
            {
                builder.ClearProviders();
                builder.AddTurboConsoleLogger();
            })
            .CreateLogger("Bootstrap");

        // `Turbo.Main migrate ...` migrates the database and exits; nothing else starts.
        var migrate = MigrateCommandLine.IsMigrateCommand(args)
            ? MigrateCommandLine.Parse(args)
            : null;

        if (migrate is { Help: true } or { Error: not null })
        {
            DatabaseStartup.PrintUsage(bootstrapLogger, migrate.Error);

            return migrate.Error is null ? DatabaseStartup.EXIT_OK : DatabaseStartup.EXIT_USAGE;
        }

        if (migrate is null)
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

        if (migrate is null)
            bootstrapLogger.LogInformation(
                "Starting {GetProjectName} {GetProductVersion}",
                GetProjectName(),
                GetProjectVersion()
            );

        var builder = Host.CreateApplicationBuilder(migrate?.HostArgs ?? args);

        builder.Configuration.AddEnvironmentVariables(prefix: "TURBO__");

        builder.AddTurboTelemetry();

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

        builder.AddOrleans();

        builder.Services.AddTurboLogging(builder);
        builder.Services.AddTurboNetworking(builder);
        builder.Services.AddTurboPlugins(builder);
        builder.Services.AddTurboDatabaseContext(builder);
        builder.Services.AddTurboEventSystem();
        builder.Services.AddTurboMessageSystem();
        builder.Services.AddTurboCrypto(builder);

        builder.Services.AddHostPlugin<AuthenticationModule>(builder);
        builder.Services.AddHostPlugin<FurnitureModule>(builder);
        builder.Services.AddHostPlugin<CatalogModule>(builder);
        builder.Services.AddHostPlugin<GamedataModule>(builder);
        builder.Services.AddHostPlugin<PlayerModule>(builder);
        builder.Services.AddHostPlugin<AchievementModule>(builder);
        builder.Services.AddHostPlugin<InventoryModule>(builder);
        builder.Services.AddHostPlugin<NavigatorModule>(builder);
        builder.Services.AddHostPlugin<GuildModule>(builder);
        builder.Services.AddHostPlugin<CommandModule>(builder);
        builder.Services.AddHostPlugin<RoomModule>(builder);
        builder.Services.AddHostPlugin<OperationsModule>(builder);
        builder.Services.AddHostPlugin<AdminModule>(builder);
        builder.Services.AddHostPlugin<WebModule>(builder);
        builder.Services.AddHostPlugin<PacketHandlersModule>(builder);

        builder.Services.AddSingleton<AssemblyProcessor>();
        builder.Services.AddSingleton<ConsoleCommandService>();

        builder.Services.AddHostedService<TurboEmulator>();

        using var host = builder.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var ct = lifetime.ApplicationStopping;

        if (migrate is not null)
            return await DatabaseStartup
                .RunCommandAsync(host.Services, migrate, bootstrapLogger, ct)
                .ConfigureAwait(false);

        try
        {
            // The emulator's own tables first: every provider and grain reads them as it starts.
            // Plugins' tables are migrated as each plugin loads.
            await DatabaseStartup.MigrateAsync(host.Services, ct).ConfigureAwait(false);

            await host.StartAsync(ct).ConfigureAwait(false);

            bootstrapLogger.LogInformation(
                "Started {GetProjectName} {GetProductVersion}",
                GetProjectName(),
                GetProjectVersion()
            );

            host.Services.GetService<ConsoleCommandService>()?.Enable();

            await host.WaitForShutdownAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (MigrationException.FindIn(ex) is { } migration)
        {
            // The message is the whole report; a stack trace would only bury it. A non-zero exit
            // lets a supervisor see that the server did not come up. A plugin's migration problem
            // surfaces from the host's start, wrapped, so it is looked for inside.
            bootstrapLogger.LogCritical("{Message}", migration.Message);

            return DatabaseStartup.EXIT_FAILED;
        }
        catch (Exception ex)
        {
            bootstrapLogger.LogCritical(ex, "Host terminated unexpectedly");
        }

        return DatabaseStartup.EXIT_OK;
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
