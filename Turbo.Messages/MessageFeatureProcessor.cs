using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking;

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
    ) { }
