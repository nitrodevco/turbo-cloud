using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Networking.Configuration;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;

namespace Turbo.Networking.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboNetworking(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        // Below a header every packet is refused; past 1 GB a packet and its length no longer
        // fit one array, and the length check itself could overflow.
        services
            .AddOptions<NetworkingConfig>()
            .Bind(builder.Configuration.GetSection(NetworkingConfig.SECTION_NAME))
            .Validate(
                x =>
                    x.MaxIncomingPacketBytes
                        is >= ClientPacketDecoder.HEADER_BYTES
                            and <= 1024 * 1024 * 1024,
                "Turbo:Networking:MaxIncomingPacketBytes must be from 2 (a packet's header) to 1 GB."
            )
            .ValidateOnStart();

        services.AddSingleton<INetworkManager, NetworkManager>();
        services.AddSingleton<IRevisionManager, RevisionManager>();
        services.AddSingleton<ISessionGateway, SessionGateway>();
        services.AddSingleton<IExtensionPacketRegistry, ExtensionPacketRegistry>();

        return services;
    }
}
