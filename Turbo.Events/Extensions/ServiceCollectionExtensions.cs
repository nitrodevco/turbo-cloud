using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Registry;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Events.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboEventSystem(this IServiceCollection services)
    {
        services.AddSingleton<IAssemblyFeatureProcessor, EventFeatureProcessor>();
        services.AddSingleton<EventInvokerFactory>();
        services.AddSingleton<EventRegistry>();
        services.AddSingleton<EventSystem>();

        return services;
    }
}
