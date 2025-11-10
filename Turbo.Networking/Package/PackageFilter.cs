using System;
using System.Buffers;
using System.Buffers.Binary;
using SuperSocket.ProtoBase;
using Turbo.Packets.Abstractions;
using Turbo.Primitives.Networking;

namespace Turbo.Networking.Package;

internal sealed class PackageFilter : PipelineFilterBase<IClientPacket>
{
    public override IClientPacket Filter(ref SequenceReader<byte> reader)
    {
        var r = reader;

        if (Context is not ISessionContext ctx || r.Remaining < 4)
            return null!;

        Span<byte> hdr = stackalloc byte[4];
        r.Sequence.Slice(r.Consumed, 4).CopyTo(hdr);

        var length = BinaryPrimitives.ReadInt32BigEndian(
            ctx.CryptoIn is not null ? ctx.CryptoIn.Peek(hdr.ToArray()) : hdr
        );

        if (r.Remaining < (length + 4))
            return null!;

        var unread = r.Sequence.Slice(r.Consumed, length + 4).ToArray();

        var body = ctx.CryptoIn is not null ? ctx.CryptoIn.Process(unread) : unread;
        var packet = new ClientPacket(-1, body);

        length = packet.PopInt();
        packet.Header = packet.PopShort();

        r.Advance(length + 4);

        reader = r;

        return packet;
    }
}
