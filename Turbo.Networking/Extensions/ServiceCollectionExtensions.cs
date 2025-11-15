using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Networking.Configuration;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;

namespace Turbo.Networking.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboNetworking(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<NetworkingConfig>(
            builder.Configuration.GetSection(NetworkingConfig.SECTION_NAME)
        );

        services.AddSingleton<INetworkManager, NetworkManager>();
        services.AddSingleton<IRevisionManager, RevisionManager>();
        services.AddSingleton<ISessionGateway, SessionGateway>();

        return services;
    }
}
