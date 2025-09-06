using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Revision20240709.EventHandlers;

namespace Turbo.Revision20240709.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboRevision20240709(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddTransient<WelcomeOnJoin>();
        services.AddTransient<PacketHandlers>();

        return services;
    }
}
