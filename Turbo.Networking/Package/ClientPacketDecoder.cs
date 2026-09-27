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

        var crypto = ctx.CryptoIn;

        // The length is peeked, not decrypted: the key stream may only advance once the whole
        // packet has arrived and is consumed below.
        Span<byte> hdr = stackalloc byte[4];
        reader.Sequence.Slice(reader.Consumed, 4).CopyTo(hdr);

        crypto?.Peek(hdr, hdr);

        int length = BinaryPrimitives.ReadInt32BigEndian(hdr);

        if (reader.Remaining < (length + 4))
            return null!;

        // The packet keeps this array as its payload, so it is the one copy a packet costs;
        // decryption happens in it rather than into a second one.
        var body = reader.Sequence.Slice(reader.Consumed, length + 4).ToArray();

        crypto?.ProcessInPlace(body);

        var packet = new ClientPacket(-1, body);

        length = packet.PopInt();
        packet.Header = packet.PopShort();

        reader.Advance(length + 4);

        return packet;
    }
}
