using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Dispatcher;

public class BoundedPacketQueue : IPacketQueue
{
    private readonly Channel<PacketEnvelope> _channel;

    public BoundedPacketQueue(IEmulatorConfig config)
    {
        _channel = Channel.CreateBounded<PacketEnvelope>(
            new BoundedChannelOptions(config.Network.DispatcherOptions.GlobalQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            }
        );
    }

    public bool TryEnqueue(PacketEnvelope envelope) => _channel.Writer.TryWrite(envelope);

    public int ApproxDepth => _channel.Reader.Count;

    public IAsyncEnumerable<PacketEnvelope> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
