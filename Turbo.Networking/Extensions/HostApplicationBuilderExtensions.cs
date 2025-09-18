using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Configuration;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;

namespace Turbo.Networking.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddTurboNetworking(this HostApplicationBuilder builder)
    {
        builder.Services.Configure<NetworkingConfig>(
            builder.Configuration.GetSection(NetworkingConfig.SECTION_NAME)
        );

        builder.Services.AddSingleton<INetworkManager, NetworkManager>();
        builder.Services.AddSingleton<PacketProcessor>();
        builder.Services.AddSingleton<IRevisionManager, RevisionManager>();

        return builder;
    }
}
