using System.Collections.Generic;
using System.Threading;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Dispatcher;

public interface IPacketQueue
{
    bool TryEnqueue(PacketEnvelope envelope);

    int ApproxDepth { get; }

    IAsyncEnumerable<PacketEnvelope> ReadAllAsync(CancellationToken ct);
}
