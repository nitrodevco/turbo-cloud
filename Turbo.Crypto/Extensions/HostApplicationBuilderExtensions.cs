using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Crypto.Configuration;

namespace Turbo.Crypto.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddTurboCrypto(this HostApplicationBuilder builder)
    {
        builder.Services.Configure<CryptoConfig>(
            builder.Configuration.GetSection(CryptoConfig.SECTION_NAME)
        );

        builder.Services.AddSingleton<IRsaService, RsaService>();
        builder.Services.AddSingleton<IDiffieService, DiffieService>();

        return builder;
    }
}
