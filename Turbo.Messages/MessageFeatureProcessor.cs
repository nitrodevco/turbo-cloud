using Turbo.Contracts.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline;

namespace Turbo.Messages;

internal sealed class MessageFeatureProcessor(
    MessageRegistry registry,
    MessageInvokerFactory invokerFactory
)
    : EnvelopeFeatureProcessor<IMessageEvent, ISessionContext, MessageContext>(
        registry,
        invokerFactory,
        typeof(IMessageHandler<>),
        typeof(IMessageBehavior<>)
    ) { }
