using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Turbo.Primitives.Packets;

/// <summary>
/// Writes an outgoing packet into an array rented from the shared pool; disposing the packet
/// returns it. Every composer sent goes through one of these, so it writes values straight into
/// the buffer rather than through a stream and a writer, and a string is encoded in place instead
/// of into a temporary array first.
///
/// Positions behave like the <c>MemoryStream</c> this replaced, so the bytes are the same: a
/// write past the end extends the packet, a write before it overwrites, and a gap left by moving
/// the position past the end is zero-filled.
/// </summary>
public sealed class ServerPacket : TurboPacket, IServerPacket
{
    private const int DEFAULT_CAPACITY = 256;

    private byte[] _buffer;
    private int _position;
    private int _length;

    public ServerPacket(int header)
        : this(header, DEFAULT_CAPACITY) { }

    public ServerPacket(int header, int initialCapacity)
        : base(header)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 16));
    }

    public int Length => _length;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public IServerPacket WriteByte(byte b)
    {
        Reserve(1)[0] = b;

        return this;
    }

    public IServerPacket WriteBoolean(bool b)
    {
        return WriteByte((byte)(b ? 1 : 0));
    }

    public IServerPacket WriteShort(short s)
    {
        BinaryPrimitives.WriteInt16BigEndian(Reserve(2), s);

        return this;
    }

    public IServerPacket WriteFloat(float f)
    {
        BinaryPrimitives.WriteSingleBigEndian(Reserve(4), f);

        return this;
    }

    public IServerPacket WriteDouble(double d)
    {
        BinaryPrimitives.WriteInt64BigEndian(Reserve(8), BitConverter.DoubleToInt64Bits(d));

        return this;
    }

    public IServerPacket WriteLong(long l)
    {
        BinaryPrimitives.WriteInt64BigEndian(Reserve(8), l);

        return this;
    }

    public IServerPacket WriteInteger(int i)
    {
        BinaryPrimitives.WriteInt32BigEndian(Reserve(4), i);

        return this;
    }

    public IServerPacket WriteString(string s)
    {
        s ??= string.Empty;

        var byteCount = Encoding.UTF8.GetByteCount(s);

        WriteShort((short)byteCount);
        Encoding.UTF8.GetBytes(s, Reserve(byteCount));

        return this;
    }

    public IServerPacket SetWriterPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        _position = position;

        return this;
    }

    public byte[] ToArray() => WrittenSpan.ToArray();

    public void Dispose()
    {
        var buffer = _buffer;

        _buffer = [];
        _position = 0;
        _length = 0;

        if (buffer.Length > 0)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    private Span<byte> Reserve(int count)
    {
        var end = _position + count;

        if (end > _buffer.Length)
            Grow(end);

        // A rented array is not zeroed, and a stream reads a skipped gap as zeros.
        if (_position > _length)
            _buffer.AsSpan(_length, _position - _length).Clear();

        var span = _buffer.AsSpan(_position, count);

        _position = end;

        if (end > _length)
            _length = end;

        return span;
    }

    private void Grow(int required)
    {
        ObjectDisposedException.ThrowIf(_buffer.Length == 0, this);

        var next = ArrayPool<byte>.Shared.Rent(Math.Max(required, _buffer.Length * 2));

        _buffer.AsSpan(0, _length).CopyTo(next);
        ArrayPool<byte>.Shared.Return(_buffer);

        _buffer = next;
    }
}
