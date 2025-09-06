using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Events.Abstractions;
using Turbo.Events.Configuration;
using Turbo.Pipeline.Abstractions.Enums;

namespace Turbo.Events.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboEvents(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<EventConfig>().Bind(cfg.GetSection(EventConfig.SECTION_NAME));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EventConfig>>().Value);

        services.AddSingleton<Channel<EventEnvelope>>(sp =>
        {
            var cfg = sp.GetRequiredService<EventConfig>();

            if (cfg.ChannelCapacity > 0)
                return Channel.CreateBounded<EventEnvelope>(
                    new BoundedChannelOptions(cfg.ChannelCapacity)
                    {
                        FullMode = cfg.Backpressure switch
                        {
                            BackpressureMode.DropNewest => BoundedChannelFullMode.DropWrite,
                            BackpressureMode.DropOldest => BoundedChannelFullMode.DropOldest,
                            BackpressureMode.Wait => BoundedChannelFullMode.Wait,
                            BackpressureMode.Fail => BoundedChannelFullMode.Wait, // we'll throw in EventBus for Fail
                        },
                        SingleReader = false,
                        SingleWriter = false,
                    }
                );

            return Channel.CreateUnbounded<EventEnvelope>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        });

        services.AddSingleton<IEventBus, EventBus>();
        services.AddHostedService(sp => (EventBus)sp.GetRequiredService<IEventBus>());

        return services;
    }
}
