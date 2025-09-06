using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Messaging.Abstractions;
using Turbo.Messaging.Configuration;
using Turbo.Pipeline.Abstractions.Enums;

namespace Turbo.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboMessaging(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<MessagingConfig>().Bind(cfg.GetSection(MessagingConfig.SECTION_NAME));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MessagingConfig>>().Value);

        services.AddSingleton<Channel<MessageEnvelope>>(sp =>
        {
            var cfg = sp.GetRequiredService<MessagingConfig>();

            if (cfg.ChannelCapacity > 0)
                return Channel.CreateBounded<MessageEnvelope>(
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

            return Channel.CreateUnbounded<MessageEnvelope>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        });

        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddHostedService(sp => (MessageBus)sp.GetRequiredService<IMessageBus>());

        return services;
    }
}
