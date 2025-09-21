using Turbo.Contracts.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline;

namespace Turbo.Messages;

public sealed class MessageRegistry : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>
{
    public MessageRegistry()
        : base((env, data) => new MessageContext { Session = data }) { }
}
