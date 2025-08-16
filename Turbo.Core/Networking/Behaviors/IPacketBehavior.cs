using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Behaviors;

public interface IPacketBehavior
{
    Task InvokeAsync(
        IPacketContext ctx,
        PacketEnvelope env,
        CancellationToken ct,
        PacketDelegate next
    );
}
