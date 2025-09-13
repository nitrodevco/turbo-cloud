using System;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Abstractions;
using Turbo.Messaging.Abstractions;
using Turbo.Messaging.Abstractions.Registry;
using Turbo.Messaging.Configuration;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core.Envelope;

namespace Turbo.Messaging;

public class MessageBus(MessagingConfig cfg, IServiceProvider root, ILogger<MessageBus> logger)
    : EnvelopeBus<MessageEnvelope, IMessageEvent, MessageContext, MessagingConfig>(
        cfg,
        root,
        logger
    ),
        IMessageBus
{
    protected override string GetKeyForEnvelope(MessageEnvelope envelope)
    {
        return envelope.Session.SessionID;
    }

    protected override MessageEnvelope CreateEnvelope(IMessageEvent interaction, object? args)
    {
        var session =
            args as ISessionContext
            ?? throw new ArgumentException("Session context is required", nameof(args));

        return new MessageEnvelope { Data = interaction!, Session = session };
    }

    protected override Type GetHandlerForType()
    {
        return typeof(IMessageHandler<>);
    }

    protected override Type GetBehaviorForType()
    {
        return typeof(IMessageBehavior<>);
    }

    protected override MessageContext CreateContextForEnvelope(
        MessageEnvelope envelope,
        IServiceProvider sp
    )
    {
        return new MessageContext { Services = sp, Session = envelope.Session };
    }
}
