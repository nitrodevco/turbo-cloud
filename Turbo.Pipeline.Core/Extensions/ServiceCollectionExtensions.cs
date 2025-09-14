using System;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Pipeline.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnvelopeSystem<TSystem, TInteraction, TContext, TMeta>(
        this IServiceCollection services,
        Func<IServiceProvider, object, TMeta, TContext> createContext
    )
        where TSystem : class
    {
        var busType = typeof(GenericBus<,,>).MakeGenericType(
            typeof(TInteraction),
            typeof(TContext),
            typeof(TMeta)
        );

        services.Add(
            new ServiceDescriptor(
                busType,
                sp => ActivatorUtilities.CreateInstance(sp, busType, createContext),
                ServiceLifetime.Singleton
            )
        );

        services.Add(
            new ServiceDescriptor(
                typeof(TSystem),
                sp => ActivatorUtilities.CreateInstance<TSystem>(sp),
                ServiceLifetime.Singleton
            )
        );

        return services;
    }
}
