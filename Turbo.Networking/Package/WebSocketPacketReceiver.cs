using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;
using Turbo.Networking.Session;
using Turbo.Primitives.Networking.Enums;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

/// <summary>
/// The WebSocket server's message handler: reads the packets out of each binary message a
/// client sends and hands them to the package handler, keeping a packet split across messages
/// in the session's receive buffer (<see cref="WebSocketSessionContext.ReceiveBuffer"/>) until
/// the rest arrives.
/// </summary>
/// <remarks>
/// The buffer never holds more than one largest packet and its length
/// (<see cref="ClientPacketDecoder.MaxUnreadBytes"/>): a message that would take it past that
/// is refused before it is copied, as a declared length past the maximum is by the decoder.
/// Without that, a client that declared a large packet and never finished it could grow the
/// buffer without bound before logging in.
/// </remarks>
internal sealed class WebSocketPacketReceiver(
    ClientPacketDecoder decoder,
    IPackageHandler<IClientPacket> packageHandler
)
{
    private readonly ClientPacketDecoder _decoder = decoder;
    private readonly IPackageHandler<IClientPacket> _packageHandler = packageHandler;

    public async ValueTask ReceiveAsync(WebSocketSession session, WebSocketPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (
            session is not WebSocketSessionContext ctx
            || ctx.ReceiveBuffer is not { } buffer
            || package.OpCode != OpCode.Binary
        )
            return;

        var unread = buffer.WrittenCount + package.Data.Length;

        if (unread > _decoder.MaxUnreadBytes)
        {
            ctx.StopReceiving();
            _decoder.Refuse(
                ctx,
                $"sent {unread} bytes of unread packet data, more than {_decoder.MaxUnreadBytes}"
            );

            return;
        }

        foreach (var segment in package.Data)
            buffer.Write(segment.Span);

        var memory = buffer.WrittenMemory;
        var consumed = 0;
        var refused = false;

        // Packets are decoded one at a time and each is handled before the next is read: the
        // handshake's handler switches decryption on, and a packet behind it in the same frame
        // must be decoded with the new key. The reader is rebuilt from the offset after each
        // await (it cannot live across one), and whatever is left is moved to the front once,
        // instead of recopying the tail after every packet.
        try
        {
            while (consumed < memory.Length)
            {
                var result = TryReadPacket(memory[consumed..], ctx, out var packet, out var read);

                if (result == ClientPacketReadResult.Refused)
                {
                    refused = true;
                    break;
                }

                if (packet is null)
                    break;

                consumed += read;

                await _packageHandler
                    .Handle(session, packet, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            // Even on a failure, so packets already handled are not handled again with the next
            // frame.
            if (refused)
            {
                ctx.StopReceiving();
            }
            else
            {
                CompactReceiveBuffer(buffer, memory, consumed);
                ctx.ShrinkReceiveBuffer();
            }
        }
    }

    // A SequenceReader is a ref struct and cannot sit in the async receive loop, so the decode
    // step lives here and reports how far it read.
    private ClientPacketReadResult TryReadPacket(
        ReadOnlyMemory<byte> unread,
        WebSocketSessionContext ctx,
        out IClientPacket? packet,
        out int consumed
    )
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(unread));
        var result = _decoder.TryRead(ref reader, ctx, out packet);

        consumed = (int)reader.Consumed;

        return result;
    }

    // Moves the unread tail of a receive buffer to its front. ResetWrittenCount keeps the backing
    // array (Clear would zero it first), and the copy handles the overlap.
    private static void CompactReceiveBuffer(
        ArrayBufferWriter<byte> buffer,
        ReadOnlyMemory<byte> written,
        int consumed
    )
    {
        if (consumed == 0)
            return;

        var remaining = written[consumed..];

        buffer.ResetWrittenCount();

        if (remaining.Length == 0)
            return;

        remaining.Span.CopyTo(buffer.GetSpan(remaining.Length));
        buffer.Advance(remaining.Length);
    }
}
