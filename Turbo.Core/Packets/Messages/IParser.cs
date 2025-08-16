using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Session;

namespace Turbo.Core.Packets.Messages;

public interface IParser
{
    public IMessageEvent Parse(IClientPacket packet);
    public Task HandleAsync(ISessionContext ctx, IClientPacket message, IPacketMessageHub messageHub, CancellationToken ct);
}