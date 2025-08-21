using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Networking.Session;

public class SessionContext : ISessionContext
{
    private readonly IChannelHandlerContext _ctx;
    private readonly Channel<IComposer> _queue;
    private readonly Task _pump;
    private readonly CancellationTokenSource _cts = new();
    private const int MAX_BATCH = 64;
    private static readonly TimeSpan MAX_BATCH_DELAY = TimeSpan.FromMilliseconds(5);

    public IChannel Channel => _ctx.Channel;

    public IChannelId ChannelId => _ctx.Channel.Id;

    public SessionContext(IChannelHandlerContext ctx, int capacity = 2048)
    {
        _ctx = ctx;
        _queue = System.Threading.Channels.Channel.CreateBounded<IComposer>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            }
        );
        _pump = Task.Run(PumpAsync);
    }

    public string RevisionId { get; private set; }
    public IRevision Revision { get; private set; }

    public long PlayerId { get; private set; }

    public bool IsAuthenticated => PlayerId != 0;

    public IRc4Service Rc4 { get; set; }

    public void SetRevision(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void AttachPlayer(long playerId)
    {
        PlayerId = playerId;
    }

    public async Task DisposeAsync()
    {
        await _ctx.CloseAsync();
    }

    public async ValueTask SendAsync(IComposer composer, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(composer, ct);
    }

    public async ValueTask SendManyAsync(
        IEnumerable<IComposer> composers,
        CancellationToken ct = default
    )
    {
        foreach (var c in composers)
            await SendAsync(c, ct);
    }

    private async Task PumpAsync()
    {
        var reader = _queue.Reader;
        var cts = _cts.Token;

        try
        {
            while (await reader.WaitToReadAsync(cts))
            {
                // batch
                var batch = new List<IComposer>(MAX_BATCH);
                if (reader.TryRead(out var first))
                    batch.Add(first);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (
                    batch.Count < MAX_BATCH
                    && sw.Elapsed < MAX_BATCH_DELAY
                    && reader.TryRead(out var item)
                )
                    batch.Add(item);

                if (batch.Count == 0)
                    continue;

                // Encode and write all; flush once
                foreach (var composer in batch)
                {
                    IServerPacket payload = null;

                    if (Revision.Serializers.TryGetValue(composer.GetType(), out var serializer))
                    {
                        payload = serializer.Serialize(Unpooled.Buffer(), composer);
                    }

                    if (payload is not null)
                    {
                        payload.Release();
                        await _ctx.WriteAsync(payload);
                    }
                }

                _ctx.Flush();
            }
        }
        catch (OperationCanceledException)
        { /* shutting down */
        }
        catch (Exception ex)
        {
            // log
            Console.Error.WriteLine($"Outbound pump failed: {ex}");
            // Optional: close channel on fatal encode errors
            // _ = _nettyChannel.CloseAsync();
        }
    }

    public void PauseReads() => Channel.Configuration.SetOption(ChannelOption.AutoRead, false);

    public void ResumeReads() => Channel.Configuration.SetOption(ChannelOption.AutoRead, true);
}
