using Microsoft.Extensions.DependencyInjection;
using Turbo.Events.Registry;
using Turbo.Pipeline;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Events.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboEventSystem(this IServiceCollection services)
    {
        services.AddSingleton<IAssemblyFeatureProcessor, EventFeatureProcessor>();
        services.AddSingleton<EnvelopeInvokerFactory<EventContext>>();
        services.AddSingleton<EventRegistry>();
        services.AddSingleton<EventSystem>();

        return services;
    }
}
