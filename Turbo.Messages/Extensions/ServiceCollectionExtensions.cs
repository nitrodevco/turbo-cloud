using Microsoft.Extensions.DependencyInjection;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Messages.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboMessageSystem(this IServiceCollection services)
    {
        services.AddSingleton<IAssemblyFeatureProcessor, MessageFeatureProcessor>();
        services.AddSingleton<EnvelopeInvokerFactory<MessageContext>>();
        services.AddSingleton<MessageRegistry>();
        services.AddSingleton<MessageSystem>();

        services.AddSingleton<IPluginMessageDirectory, PluginMessageDirectory>();
        services.AddSingleton<IAssemblyFeatureProcessor, PluginMessageFeatureProcessor>();
        services.AddHostedService<PluginMessageBroadcast>();

        return services;
    }
}
