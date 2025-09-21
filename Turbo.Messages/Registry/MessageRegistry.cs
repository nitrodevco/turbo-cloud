using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>
{
    public MessageRegistry()
        : base((env, data) => new MessageContext { Session = data }) { }
}
