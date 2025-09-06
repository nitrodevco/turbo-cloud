using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Messaging.Abstractions;
using Turbo.Messaging.Configuration;

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

        services.AddSingleton<IMessageBus, MessageBus>();

        return services;
    }
}
