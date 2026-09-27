using System;

namespace Turbo.Primitives.Packets;

/// <summary>
/// An outgoing packet being written. Its bytes live in a pooled buffer, so whoever takes the
/// packet from a serializer copies the bytes out (<see cref="ToArray"/> or
/// <see cref="WrittenSpan"/>) and then disposes it.
/// </summary>
public interface IServerPacket : ITurboPacket, IDisposable
{
    public int Length { get; }

    /// <summary>The bytes written so far. Only valid until the packet is disposed.</summary>
    public ReadOnlySpan<byte> WrittenSpan { get; }

    IServerPacket WriteByte(byte b);

    IServerPacket WriteBoolean(bool b);

    IServerPacket WriteShort(short s);

    IServerPacket WriteFloat(float f);

    IServerPacket WriteDouble(double d);

    IServerPacket WriteLong(long l);
    IServerPacket WriteInteger(int i);

    IServerPacket WriteString(string s);
    IServerPacket SetWriterPosition(int position);
    public byte[] ToArray();
}
