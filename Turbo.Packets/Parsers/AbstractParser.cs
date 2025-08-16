namespace Turbo.Packets.Parsers;

using System.Threading;
using System.Threading.Tasks;

using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

public abstract class AbstractParser<T> : IParser
    where T : IMessageEvent
{
    public virtual async Task HandleAsync(ISessionContext ctx, IClientPacket message, IPacketMessageHub messageHub, CancellationToken ct)
    {
        await messageHub.PublishAsync((T)Parse(message), ctx);
    }

    public abstract IMessageEvent Parse(IClientPacket packet);
}
