namespace Turbo.Core.Networking.Dispatcher;

using System.Collections.Generic;
using System.Threading;

using Turbo.Core.Packets.Messages;

public interface IPacketQueue
{
    bool TryEnqueue(PacketEnvelope envelope);

    int ApproxDepth { get; }

    IAsyncEnumerable<PacketEnvelope> ReadAllAsync(CancellationToken ct);
}
