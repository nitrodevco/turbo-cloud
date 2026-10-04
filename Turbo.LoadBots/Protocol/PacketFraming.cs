using System;
using System.Buffers.Binary;

namespace Turbo.LoadBots.Protocol;

/// <summary>One message the server sent: its header id and the body after it.</summary>
public sealed record ServerMessage(int Header, ReadOnlyMemory<byte> Body)
{
    public PacketReader Reader() => new(Body);
}

/// <summary>
/// The frame both directions use: an int32 length of what follows, an int16 header, the body.
/// </summary>
public static class PacketFraming
{
    public const int LENGTH_SIZE = 4;
    public const int HEADER_SIZE = 2;

    public static byte[] Frame(int header, ReadOnlySpan<byte> body)
    {
        var frame = new byte[LENGTH_SIZE + HEADER_SIZE + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, HEADER_SIZE + body.Length);
        BinaryPrimitives.WriteInt16BigEndian(frame.AsSpan(LENGTH_SIZE), (short)header);
        body.CopyTo(frame.AsSpan(LENGTH_SIZE + HEADER_SIZE));

        return frame;
    }

    /// <summary>
    /// Takes one whole message off the front of <paramref name="buffer"/>, or returns false
    /// (consuming nothing) when the buffer does not yet hold one.
    /// </summary>
    public static bool TryReadFrame(
        ReadOnlySpan<byte> buffer,
        out ServerMessage? message,
        out int consumed
    )
    {
        message = null;
        consumed = 0;

        if (buffer.Length < LENGTH_SIZE)
            return false;

        var length = BinaryPrimitives.ReadInt32BigEndian(buffer);

        if (length < HEADER_SIZE)
            throw new FormatException($"Frame length {length} is shorter than a header.");

        if (buffer.Length < LENGTH_SIZE + length)
            return false;

        var header = BinaryPrimitives.ReadInt16BigEndian(buffer[LENGTH_SIZE..]);
        var body = buffer.Slice(LENGTH_SIZE + HEADER_SIZE, length - HEADER_SIZE).ToArray();

        message = new ServerMessage(header, body);
        consumed = LENGTH_SIZE + length;

        return true;
    }
}
