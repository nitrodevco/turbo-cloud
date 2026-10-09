using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Tcp;

internal sealed class TcpFilter(IClientPacketDecoder decoder) : PipelineFilterBase<IClientPacket>
{
    private readonly IClientPacketDecoder _decoder = decoder;

    public override IClientPacket Filter(ref SequenceReader<byte> reader)
    {
        if (Context is not ISessionContext ctx)
            return null!;

        var r = reader;
        var packet = _decoder.TryRead(ref r, ctx);

        if (packet is null)
            return null!;

        reader = r;

        // Any packet proves the client is there, a Pong or otherwise; see SessionHeartbeat. Done
        // here because SuperSocket frames the next packet while the one before is still handled.
        ctx.MarkReceived();

        return packet;
    }
}
