using System;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Pipeline.Extensions;

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

        services.AddSingleton(
            busType,
            sp => ActivatorUtilities.CreateInstance(sp, busType, createContext)
        );

        services.AddSingleton(
            typeof(TSystem),
            sp => ActivatorUtilities.CreateInstance<TSystem>(sp)
        );

        return services;
    }
}
