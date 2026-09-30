using System;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

namespace Turbo.Messages;

internal sealed class MessageFeatureProcessor(
    MessageRegistry registry,
    EnvelopeInvokerFactory<MessageContext> invokerFactory
)
    : EnvelopeFeatureProcessor<IMessageEvent, ISessionContext, MessageContext>(
        registry,
        invokerFactory,
        typeof(IMessageHandler<>),
        typeof(IMessageBehavior<>)
    )
{
    protected override HandlerInvoker<MessageContext> DecorateHandler(
        Type handlerType,
        IServiceProvider sp,
        HandlerInvoker<MessageContext> invoker
    )
    {
        // Resolved on the first gated packet, not here, which can run while the host is built.
        IGrainFactory? grainFactory = null;

        return PermissionGate.Wrap(
            handlerType,
            (playerId, node, ct) =>
                (grainFactory ??= sp.GetRequiredService<IGrainFactory>()).HasPermissionAsync(
                    playerId,
                    node,
                    ct
                ),
            invoker
        );
    }
}
