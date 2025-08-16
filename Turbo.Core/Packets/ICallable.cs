namespace Turbo.Core.Packets;

using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

public interface ICallable<T>
    where T : IMessageEvent
{
    public bool Call(T message, ISessionContext ctx);
}
