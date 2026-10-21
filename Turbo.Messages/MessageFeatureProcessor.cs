using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

namespace Turbo.Messages;

internal sealed class MessageFeatureProcessor(
    MessageRegistry registry,
    EnvelopeInvokerFactory<MessageContext> invokerFactory,
    ILoggerFactory loggerFactory
)
    : EnvelopeFeatureProcessor<IMessageEvent, ISessionContext, MessageContext>(
        registry,
        invokerFactory,
        typeof(IMessageHandler<>),
        typeof(IMessageBehavior<>)
    )
{
    private readonly ILogger _signInLogger = loggerFactory.CreateLogger(typeof(SignInGate));

    protected override HandlerInvoker<MessageContext> DecorateHandler(
        Type handlerType,
        IServiceProvider sp,
        HandlerInvoker<MessageContext> invoker
    )
    {
        // Resolved on the first gated packet, not here, which can run while the host is built.
        IGrainFactory? grainFactory = null;

        // Signed in first: a session that has not logged in is dropped before a permission is
        // asked about.
        return SignInGate.Wrap(
            handlerType,
            _signInLogger,
            PermissionGate.Wrap(
                handlerType,
                (playerId, node, ct) =>
                    (grainFactory ??= sp.GetRequiredService<IGrainFactory>()).HasPermissionAsync(
                        playerId,
                        node,
                        ct
                    ),
                invoker
            )
        );
    }
}
