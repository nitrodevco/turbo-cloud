using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Behaviors;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Behaviors;

public delegate Task PacketDelegate(IPacketContext ctx, PacketEnvelope env, CancellationToken ct);
