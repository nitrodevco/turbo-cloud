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

        return packet;
    }
}
