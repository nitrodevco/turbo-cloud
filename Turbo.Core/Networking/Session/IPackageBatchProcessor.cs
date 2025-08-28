using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Session;

public interface IPacketProcessor
{
    public Task ProcessClientPacket(
        ISessionContext ctx,
        IClientPacket clientPacket,
        CancellationToken ct = default
    );
    public Task ProcessComposer(
        ISessionContext ctx,
        IComposer composer,
        CancellationToken ct = default
    );
}
