using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Database.Extensions;
using Turbo.Events.Abstractions.Registry;
using Turbo.Events.Extensions;
using Turbo.Messaging.Abstractions.Registry;
using Turbo.Messaging.Extensions;
using Turbo.Networking.Extensions;
using Turbo.Plugins.Extensions;
using Turbo.Revision20240709.Extensions;

namespace Turbo.Main.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureTurbo(this IHostBuilder host)
    {
        host.ConfigureServices(
            (ctx, services) =>
            {
                services.UseTurboPlugins(ctx.Configuration);

                services.AddTurboDatabase(ctx.Configuration);
                services.AddTurboNetworking(ctx.Configuration);
                services.AddTurboEvents(ctx.Configuration);
                services.AddTurboMessaging(ctx.Configuration);

                services.AddTurboDefaultRevision(ctx.Configuration);
                services.AddTurboRevision20240709(ctx.Configuration);

                // Emulator
                services.AddHostedService<TurboEmulator>();
            }
        );

        return host;
    }
}
