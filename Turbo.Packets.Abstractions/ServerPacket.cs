using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Turbo.Packets.Abstractions;

public class ServerPacket(int header) : TurboPacket(header), IServerPacket
{
    public BinaryWriter Writer { get; } = new(new MemoryStream());
    public MemoryStream Stream => (MemoryStream)Writer.BaseStream;

    public int Length => (int)Writer.BaseStream.Length;

    public IServerPacket WriteByte(byte b)
    {
        Writer.Write(b);
        _logger.Append($"{{u:{b}}}");

        return this;
    }

    public IServerPacket WriteBoolean(bool b)
    {
        return WriteByte((byte)(b ? 1 : 0));
    }

    public IServerPacket WriteShort(short s)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(b, s);

        Writer.Write(b);

        _logger.Append($"{{s:{s}}}");
        return this;
    }

    public IServerPacket WriteDouble(double d)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, BitConverter.DoubleToInt64Bits(d));

        Writer.Write(b);
        _logger.Append($"{{d:{d}}}");

        return this;
    }

    public IServerPacket WriteLong(long l)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, l);

        Writer.Write(b);
        _logger.Append($"{{l:{l}}}");

        return this;
    }

    public IServerPacket WriteInteger(int i)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, i);

        Writer.Write(b);
        _logger.Append($"{{i:{i}}}");

        return this;
    }

    public IServerPacket WriteString(string s)
    {
        var data = Encoding.UTF8.GetBytes(s ?? string.Empty);

        WriteShort((short)data.Length);
        Writer.Write(data);

        _logger.Append($"{{s:\"{s}\"}}");

        return this;
    }

    public IServerPacket SetWriterPosition(int position)
    {
        Stream.Position = position;

        return this;
    }

    public byte[] ToArray() => Stream.ToArray();
}
