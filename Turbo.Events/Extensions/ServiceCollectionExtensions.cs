using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Events.Abstractions;
using Turbo.Events.Configuration;

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

        services.AddSingleton<IEventBus, EventBus>();

        return services;
    }
}
