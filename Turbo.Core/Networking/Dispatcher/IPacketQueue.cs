using System.Collections.Generic;
using System.Threading;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Dispatcher;

public interface IPacketQueue
{
    int ApproxDepth { get; }
    int ShardCount { get; }
    public bool TryEnqueue(PacketEnvelope envelope);
    IAsyncEnumerable<PacketEnvelope> ReadShardAsync(int shardIndex, CancellationToken ct);
}
