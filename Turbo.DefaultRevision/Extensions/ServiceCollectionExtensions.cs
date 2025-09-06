using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.DefaultRevision;

namespace Turbo.Revision20240709.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboDefaultRevision(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        services.AddScoped<PacketHandlers>();

        return services;
    }
}
