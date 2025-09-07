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
using Turbo.Revision20240709.Extensions;

namespace Turbo.Main.Extensions;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureTurbo(this IHostBuilder host)
    {
        host.ConfigureServices(
            (ctx, services) =>
            {
                services.AddTurboDatabase(ctx.Configuration);
                services.AddTurboNetworking(ctx.Configuration);
                services.AddTurboEvents(ctx.Configuration);
                services.AddTurboMessaging(ctx.Configuration);

                services.AddTurboDefaultRevision(ctx.Configuration);
                services.AddTurboRevision20240709(ctx.Configuration);

                // Emulator
                services.AddHostedService<TurboEmulator>();

                services.BridgeConcretions(
                    iface =>
                        iface.IsGenericType
                        && iface.GetGenericTypeDefinition() == typeof(IEventHandler<>),
                    iface =>
                        iface.IsGenericType
                        && iface.GetGenericTypeDefinition() == typeof(IEventBehavior<>),
                    iface =>
                        iface.IsGenericType
                        && iface.GetGenericTypeDefinition() == typeof(IMessageHandler<>),
                    iface =>
                        iface.IsGenericType
                        && iface.GetGenericTypeDefinition() == typeof(IMessageBehavior<>)
                );
            }
        );

        return host;
    }

    public static IServiceCollection BridgeConcretions(
        this IServiceCollection services,
        params Func<Type, bool>[] interfacePredicates
    )
    {
        var snapshot = services.ToArray();

        foreach (var d in snapshot)
        {
            var impl =
                d.ImplementationType
                ?? (d.ServiceType.IsClass && !d.ServiceType.IsAbstract ? d.ServiceType : null);

            if (impl is null || impl.IsAbstract || impl.IsInterface)
                continue;

            foreach (var iface in impl.GetInterfaces())
            {
                if (interfacePredicates.All(pred => !pred(iface)))
                    continue;

                var alreadyMapped = services.Any(sd =>
                    sd.ServiceType == iface
                    && (
                        sd.ImplementationType == impl
                        || (sd.ImplementationFactory is not null && sd.ImplementationType is null)
                    )
                );
                if (alreadyMapped)
                    continue;

                if (impl.IsGenericTypeDefinition)
                {
                    services.TryAddEnumerable(ServiceDescriptor.Describe(iface, impl, d.Lifetime));
                }
                else
                {
                    services.Add(
                        new ServiceDescriptor(iface, sp => sp.GetRequiredService(impl), d.Lifetime)
                    );
                }
            }
        }

        return services;
    }
}
