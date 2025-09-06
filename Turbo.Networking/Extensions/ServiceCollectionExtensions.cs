using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Encryption;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Configuration;
using Turbo.Networking.Encryption;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;

namespace Turbo.Networking.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboNetworking(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddOptions<NetworkingConfig>().Bind(cfg.GetSection(NetworkingConfig.SECTION_NAME));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NetworkingConfig>>().Value);

        services.AddSingleton<INetworkManager, NetworkManager>();
        services.AddSingleton<PacketProcessor>();
        services.AddSingleton<IRsaService, RsaService>();
        services.AddSingleton<IDiffieService, DiffieService>();
        services.AddSingleton<IRevisionManager, RevisionManager>();

        return services;
    }
}
