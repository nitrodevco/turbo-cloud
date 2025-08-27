using System;
using System.Buffers.Binary;
using System.Text;
using Turbo.Core.Networking.Protocol;

namespace Turbo.Networking.Protocol;

public class ClientPacket(int header, ReadOnlyMemory<byte> payload, bool littleEndian = false)
    : IClientPacket
{
    private ReadOnlyMemory<byte> _payload = payload;
    private int _pos = 0;
    private bool _littleEndian = littleEndian;

    public int Header { get; } = header;
    public int Remaining => _payload.Length - _pos;
    public bool End => _pos >= _payload.Length;

    public byte PopByte()
    {
        Ensure(1);
        return _payload.Span[_pos++];
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
        short v = _littleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(_payload.Span.Slice(_pos, 2))
            : BinaryPrimitives.ReadInt16BigEndian(_payload.Span.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public ushort PopUShort()
    {
        Ensure(2);
        ushort v = _littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(_payload.Span.Slice(_pos, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(_payload.Span.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public int PopInt()
    {
        Ensure(4);
        int v = _littleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(_payload.Span.Slice(_pos, 4))
            : BinaryPrimitives.ReadInt32BigEndian(_payload.Span.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public long PopLong()
    {
        Ensure(8);
        long v = _littleEndian
            ? BinaryPrimitives.ReadInt64LittleEndian(_payload.Span.Slice(_pos, 8))
            : BinaryPrimitives.ReadInt64BigEndian(_payload.Span.Slice(_pos, 8));
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
