using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Registry;

namespace Turbo.Events.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlobalEventBus(
        this IServiceCollection services,
        IEnumerable<Assembly> asms,
        Action<IEventBusConfig>? configure = null
    )
    {
        var opts = new IEventBusConfig();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        var reg = EventRegistry.Build(asms);
        services.AddSingleton(reg);

        var ch = CreateChannel(opts);
        services.AddSingleton(ch);

        // bus + pump
        services.AddSingleton<IEventBus, EventBus>();
        services.AddHostedService<EventProcessor>();

        // discover and register handler/behavior services
        RegisterDiscoveredServices(services, asms);

        return services;

        static Channel<EventEnvelope> CreateChannel(IEventBusConfig o)
        {
            var ch = Channel.CreateBounded<EventEnvelope>(
                new BoundedChannelOptions(o.ChannelCapacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                }
            );

            return ch;
        }

        static void RegisterDiscoveredServices(
            IServiceCollection services,
            IEnumerable<Assembly> assemblies
        )
        {
            var all = assemblies
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t is not null)!;
                    }
                })
                .Where(t => t is not null && !t.IsAbstract && !t.IsInterface)!;

            foreach (var t in all)
            {
                foreach (var i in t.GetInterfaces())
                {
                    if (!i.IsGenericType)
                        continue;

                    var definition = i.GetGenericTypeDefinition();

                    if (
                        definition != typeof(IEventHandler<>)
                        && definition != typeof(IEventBehavior<>)
                    )
                        continue;

                    services.AddScoped(i, t);
                }
            }
        }
    }
}
