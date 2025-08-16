using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Packets;

public interface ICallable<T> where T : IMessageEvent
{
    public bool Call(T message, ISessionContext ctx);
}