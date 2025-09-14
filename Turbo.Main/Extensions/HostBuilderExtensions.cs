using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Abstractions;
using Turbo.Database.Extensions;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Messages;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Extensions;
using Turbo.Pipeline.Extensions;
using Turbo.Plugins;
using Turbo.Plugins.Extensions;

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

                services.AddEnvelopeSystem<EventSystem, IEvent, EventContext, object>(
                    (sp, env, data) => new EventContext { ServiceProvider = sp }
                );

                services.AddEnvelopeSystem<
                    MessageSystem,
                    IMessageEvent,
                    MessageContext,
                    ISessionContext
                >(
                    (sp, env, session) =>
                        new MessageContext { ServiceProvider = sp, Session = session }
                );

                // Emulator
                services.AddHostedService<TurboEmulator>();
            }
        );

        return host;
    }
}
