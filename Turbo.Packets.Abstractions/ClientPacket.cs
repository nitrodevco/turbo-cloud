using System;
using System.Buffers.Binary;
using System.Text;

namespace Turbo.Packets.Abstractions;

public class ClientPacket(int header, ReadOnlyMemory<byte> payload)
    : TurboPacket(header),
        IClientPacket
{
    private ReadOnlyMemory<byte> _payload = payload;
    private int _pos = 0;

    public int Remaining => _payload.Length - _pos;
    public bool End => _pos >= _payload.Length;

    public byte PopByte()
    {
        Ensure(1);
        var b = _payload.Span[_pos++];
        return b;
    }

    public byte[] PopBytes(int count)
    {
        Ensure(count);
        var arr = _payload.Span.Slice(_pos, count).ToArray();
        _pos += count;
        return arr;
    }

    public bool PopBoolean()
    {
        return PopByte() != 0;
    }

    public short PopShort()
    {
        Ensure(2);
        short v = BinaryPrimitives.ReadInt16BigEndian(_payload.Span.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public ushort PopUShort()
    {
        Ensure(2);
        ushort v = BinaryPrimitives.ReadUInt16BigEndian(_payload.Span.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public int PopInt()
    {
        Ensure(4);
        int v = BinaryPrimitives.ReadInt32BigEndian(_payload.Span.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public long PopLong()
    {
        Ensure(8);
        long v = BinaryPrimitives.ReadInt64BigEndian(_payload.Span.Slice(_pos, 8));
        _pos += 8;
        return v;
    }

    public string PopString(Encoding? encoding = null)
    {
        var len = PopUShort();
        Ensure(len);
        encoding ??= Encoding.UTF8;
        string s = encoding.GetString(_payload.Span.Slice(_pos, len));
        _pos += len;
        return s;
    }

    private void Ensure(int count)
    {
        if (_pos + count > _payload.Length)
            throw new InvalidOperationException(
                $"Not enough data: need {count}, remaining {Remaining}"
            );
    }
}
