using System;
using System.Buffers;
using System.Buffers.Binary;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

internal sealed class ClientPacketDecoder : IClientPacketDecoder
{
    public IClientPacket TryRead(ref SequenceReader<byte> reader, ISessionContext ctx)
    {
        if (reader.Remaining < 4)
            return null!;

        Span<byte> hdr = stackalloc byte[4];
        reader.Sequence.Slice(reader.Consumed, 4).CopyTo(hdr);

        int length = BinaryPrimitives.ReadInt32BigEndian(
            ctx.CryptoIn is not null ? ctx.CryptoIn.Peek(hdr.ToArray()) : hdr
        );

        if (reader.Remaining < (length + 4))
            return null!;

        var unread = reader.Sequence.Slice(reader.Consumed, length + 4).ToArray();
        var body = ctx.CryptoIn is not null ? ctx.CryptoIn.Process(unread) : unread;
        var packet = new ClientPacket(-1, body);

        length = packet.PopInt();
        packet.Header = packet.PopShort();

        reader.Advance(length + 4);

        return packet;
    }
}
