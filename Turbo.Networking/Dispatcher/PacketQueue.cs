using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Dispatcher;

public class PacketQueue : IPacketQueue
{
    private readonly Channel<PacketEnvelope>[] _shards;
    private readonly int _mask;

    public PacketQueue(IEmulatorConfig config)
    {
        // Choose a power-of-two shard count (usually == worker count)
        var workers = Math.Max(1, config.Network.DispatcherOptions.Workers);
        ShardCount = 1;
        while (ShardCount < workers)
            ShardCount <<= 1;
        _mask = ShardCount - 1;

        var perShardCapacity = Math.Max(
            512,
            config.Network.DispatcherOptions.GlobalQueueCapacity / ShardCount
        );

        _shards = new Channel<PacketEnvelope>[ShardCount];
        for (int i = 0; i < ShardCount; i++)
        {
            _shards[i] = Channel.CreateBounded<PacketEnvelope>(
                new BoundedChannelOptions(perShardCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true, // single consumer per shard
                    SingleWriter = false, // multiple producers enqueue into this shard
                }
            );
        }
    }

    public int ShardCount { get; }

    private int ShardOf(PacketEnvelope env)
    {
        // Fast, stable-ish hash → shard index
        var h = env.ChannelId.GetHashCode();
        return h & _mask;
    }

    public bool TryEnqueue(PacketEnvelope envelope)
    {
        var shard = _shards[ShardOf(envelope)];
        // TryWrite avoids async state machines in hot path
        return shard.Writer.TryWrite(envelope);
    }

    public int ApproxDepth => _shards.Sum(s => s.Reader.Count);

    public IAsyncEnumerable<PacketEnvelope> ReadShardAsync(int shardIndex, CancellationToken ct) =>
        _shards[shardIndex].Reader.ReadAllAsync(ct);
}
