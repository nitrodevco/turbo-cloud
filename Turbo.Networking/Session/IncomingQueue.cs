using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SuperSocket.Connection;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Session;

public class IncomingQueue
{
    private readonly ISessionContext _session;
    private readonly INetworkIncomingQueueConfig _config;

    private Channel<IClientPacket> _channel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public int Count => _channel.Reader.Count;

    public IncomingQueue(ISessionContext ctx, INetworkIncomingQueueConfig config)
    {
        _session = ctx;
        _config = config;

        _channel = CreateChannel(SessionDropType.Wait);
    }

    private Channel<IClientPacket> CreateChannel(SessionDropType policy)
    {
        var full = policy switch
        {
            SessionDropType.Wait => BoundedChannelFullMode.Wait,
            SessionDropType.DropOldest => BoundedChannelFullMode.DropOldest,
            _ => BoundedChannelFullMode.DropOldest,
        };

        return Channel.CreateBounded<IClientPacket>(
            new BoundedChannelOptions(_config.MaxQueue)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = full,
            }
        );
    }

    public void Start() => _loop = Task.Run(ConsumeLoopAsync);

    public async Task StopAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        if (_loop is not null)
            await _loop;
    }

    /// Enqueue from UsePackageHandler
    public async ValueTask EnqueueAsync(IClientPacket packet, CancellationToken ct)
    {
        // hard abuse guard
        if (Count > _config.HardLimit)
        {
            // optional: send “server busy” and close
            await _session.CloseAsync(CloseReason.SocketError);

            return;
        }

        // normal mode: await room (natural TCP backpressure)
        await _channel.Writer.WriteAsync(packet, ct);
    }

    private async Task ConsumeLoopAsync()
    {
        var reader = _channel.Reader;
        var batch = new List<IClientPacket>(_config.MaxBatch);

        try
        {
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                if (!reader.TryRead(out var first))
                    continue;

                batch.Clear();
                batch.Add(first);

                using var latencyCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                var delay = Task.Delay(_config.MaxLatency, latencyCts.Token);

                // drain ready items
                while (batch.Count < _config.MaxBatch && reader.TryRead(out var next))
                    batch.Add(next);

                // coalesce a tiny window for more
                if (batch.Count < _config.MaxBatch)
                {
                    var more = await Task.WhenAny(
                        reader.WaitToReadAsync(_cts.Token).AsTask(),
                        delay
                    );
                    if (more != delay)
                    {
                        while (batch.Count < _config.MaxBatch && reader.TryRead(out var next2))
                            batch.Add(next2);
                    }
                }

                latencyCts.Cancel();

                await _session.ProcessPacketBatchAsync(batch, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // log and close the session to avoid wedging
            Console.Error.WriteLine($"Incoming consume crash sid={_session.SessionID}: {ex}");
            await _session.CloseAsync(CloseReason.ProtocolError);
        }
    }
}
