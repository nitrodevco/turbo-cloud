using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Pipeline;

public interface IProcessingPipeline
{
    Task ProcessAsync(IPacketContext ctx, PacketEnvelope env, CancellationToken ct);
}
