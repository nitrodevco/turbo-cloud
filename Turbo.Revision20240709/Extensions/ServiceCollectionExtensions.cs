using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Revision20240709.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboRevision20240709(
        this IServiceCollection services,
        IConfiguration cfg
    )
    {
        return services;
    }
}
