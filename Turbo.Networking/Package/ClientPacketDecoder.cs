using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Configuration;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

internal sealed class ClientPacketDecoder(
    NetworkingConfig config,
    ILogger<ClientPacketDecoder> logger
) : IClientPacketDecoder
{
    // Every packet is a 4-byte length, then what it counts: a 2-byte header and the body.
    private const int LENGTH_BYTES = 4;
    internal const int HEADER_BYTES = 2;

    private readonly int _maxPacketBytes = config.MaxIncomingPacketBytes;
    private readonly ILogger<ClientPacketDecoder> _logger = logger;

    /// <summary>
    /// The most a connection ever needs to hold unread: one largest packet and its length.
    /// </summary>
    public int MaxUnreadBytes => _maxPacketBytes + LENGTH_BYTES;

    public ClientPacketReadResult TryRead(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        out IClientPacket? packet
    )
    {
        packet = null;

        if (reader.Remaining < LENGTH_BYTES)
            return ClientPacketReadResult.Incomplete;

        var crypto = ctx.CryptoIn;

        // The length is peeked, not decrypted: the key stream may only advance once the whole
        // packet has arrived and is consumed below.
        Span<byte> hdr = stackalloc byte[LENGTH_BYTES];
        reader.Sequence.Slice(reader.Consumed, LENGTH_BYTES).CopyTo(hdr);

        crypto?.Peek(hdr, hdr);

        int length = BinaryPrimitives.ReadInt32BigEndian(hdr);

        // Checked before anything waits on it: a negative length or one past the maximum would
        // throw out of the slice below, or keep the connection buffering for a packet that
        // never ends. Nothing is consumed, so the key stream stays where it was.
        if (length < HEADER_BYTES || length > _maxPacketBytes)
        {
            Refuse(
                ctx,
                $"declared a packet of {length} bytes, outside {HEADER_BYTES} to {_maxPacketBytes}"
            );

            return ClientPacketReadResult.Refused;
        }

        if (reader.Remaining < length + LENGTH_BYTES)
            return ClientPacketReadResult.Incomplete;

        // The packet keeps this array as its payload, so it is the one copy a packet costs;
        // decryption happens in it rather than into a second one.
        var body = reader.Sequence.Slice(reader.Consumed, length + LENGTH_BYTES).ToArray();

        crypto?.ProcessInPlace(body);

        var clientPacket = new ClientPacket(-1, body);

        length = clientPacket.PopInt();
        clientPacket.Header = clientPacket.PopShort();

        reader.Advance(length + LENGTH_BYTES);

        packet = clientPacket;

        return ClientPacketReadResult.Packet;
    }

    /// <summary>
    /// Logs why a client's data is refused and closes its session. The caller reads nothing more
    /// from it, so this is logged once per connection.
    /// </summary>
    public void Refuse(ISessionContext ctx, string reason)
    {
        _logger.LogWarning("Session {SessionKey} {Reason}; closing it", ctx.SessionKey, reason);

        ctx.CloseSessionAsync(reason)
            .LogAndForget(_logger, "close a session whose packet data was refused");
    }
}
