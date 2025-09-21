using Microsoft.Extensions.DependencyInjection;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Messages.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageSystem(this IServiceCollection services)
    {
        services.AddSingleton<IAssemblyFeatureProcessor, MessageFeatureProcessor>();
        services.AddSingleton<MessageInvokerFactory>();
        services.AddSingleton<MessageRegistry>();
        services.AddSingleton<MessageSystem>();

        return services;
    }
}
