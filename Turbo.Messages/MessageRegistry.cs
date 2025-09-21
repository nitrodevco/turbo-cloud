using Turbo.Contracts.Abstractions;
using Turbo.Messages.Registry;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline;

namespace Turbo.Messages;

public sealed class MessageRegistry : GenericHost<IMessageEvent, MessageContext, ISessionContext>
{
    public MessageRegistry()
        : base((env, data) => new MessageContext { Session = data }) { }
}
