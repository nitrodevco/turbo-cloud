using System;
using Microsoft.Extensions.Logging;
using Turbo.Messages.Registry;
using Turbo.Pipeline;
using Turbo.Pipeline.Delegates;
using Turbo.Primitives.Networking;

namespace Turbo.Messages;

internal sealed class MessageFeatureProcessor(
    MessageRegistry registry,
    EnvelopeInvokerFactory<MessageContext> invokerFactory,
    ILoggerFactory loggerFactory,
    IServiceProvider host
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
    ) => MessageGates.Wrap(handlerType, host, _signInLogger, invoker);
}
