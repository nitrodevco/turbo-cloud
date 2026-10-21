using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Enums;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Tcp;

// SuperSocket makes one filter per connection, so the refusal below is that connection's.
internal sealed class TcpFilter(IClientPacketDecoder decoder) : PipelineFilterBase<IClientPacket>
{
    private readonly IClientPacketDecoder _decoder = decoder;

    private bool _refused;

    public override IClientPacket Filter(ref SequenceReader<byte> reader)
    {
        if (Context is not ISessionContext ctx)
            return null!;

        // The decoder has refused this connection's data and is closing it: whatever else
        // arrives before the close is dropped rather than buffered or decoded.
        if (_refused)
        {
            reader.AdvanceToEnd();

            return null!;
        }

        var r = reader;
        var result = _decoder.TryRead(ref r, ctx, out var packet);

        if (result == ClientPacketReadResult.Refused)
        {
            _refused = true;
            reader.AdvanceToEnd();

            return null!;
        }

        if (packet is null)
            return null!;

        reader = r;

        // Any packet proves the client is there, a Pong or otherwise; see SessionHeartbeat. Done
        // here because SuperSocket frames the next packet while the one before is still handled.
        ctx.MarkReceived();

        return packet;
    }
}
