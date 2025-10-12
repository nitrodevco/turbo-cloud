using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Crypto.Configuration;

namespace Turbo.Crypto.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboCrypto(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<CryptoConfig>(
            builder.Configuration.GetSection(CryptoConfig.SECTION_NAME)
        );

        services.AddSingleton<RsaService>();
        services.AddSingleton<DiffieService>();

        return services;
    }
}
