using Microsoft.Extensions.DependencyInjection;
using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Primitives.Actor;
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

        return services;
    }

    public static ActionContext ToActionContext(this MessageContext ctx) =>
        ActionContextFactory.ForPlayer(ctx.Session.SessionKey, ctx.PlayerId, ctx.RoomId);
}
