using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Abstractions;
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Logging.Extensions;
using Turbo.Messages;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Extensions;
using Turbo.Pipeline.Extensions;
using Turbo.Plugins.Extensions;

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
            bootstrapLogger.LogInformation("=============================");
        }

        builder.AddTurboLogging();
        builder.Services.AddTurboDatabaseContext(builder);
        builder.AddTurboNetworking();
        builder.Services.AddEnvelopeSystem<EventSystem, IEvent, EventContext, object>(
            (sp, env, data) => new EventContext { ServiceProvider = sp }
        );
        builder.Services.AddEnvelopeSystem<
            MessageSystem,
            IMessageEvent,
            MessageContext,
            ISessionContext
        >((sp, env, session) => new MessageContext { ServiceProvider = sp, Session = session });

        builder.AddTurboPlugins();
        builder.Services.AddHostedService<TurboEmulator>();

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
