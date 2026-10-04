using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Turbo.LoadBots.Protocol;

/// <summary>
/// Reads the body of one server message, in the order the revision serializers wrote it.
/// Reading past the end throws, so a decoder that drifted from its serializer fails loudly.
/// </summary>
public sealed class PacketReader(ReadOnlyMemory<byte> body)
{
    private readonly ReadOnlyMemory<byte> _body = body;
    private int _position;

    public int Remaining => _body.Length - _position;

    public int Int()
    {
        var value = BinaryPrimitives.ReadInt32BigEndian(Take(4));

        return value;
    }

    public short Short() => BinaryPrimitives.ReadInt16BigEndian(Take(2));

    public byte Byte() => Take(1)[0];

    public bool Bool() => Take(1)[0] != 0;

    public float Float() => BinaryPrimitives.ReadSingleBigEndian(Take(4));

    public string String()
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(Take(2));

        return Encoding.UTF8.GetString(Take(length));
    }

    /// <summary>A number the server writes as text (heights and altitudes).</summary>
    public double NumberString()
    {
        var text = String();

        return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value
            )
                ? value
            : double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                ? value
            : 0;
    }

    public int[] Ints()
    {
        var count = Count();
        var values = new int[count];

        for (var i = 0; i < count; i++)
            values[i] = Int();

        return values;
    }

    public string[] Strings()
    {
        var count = Count();
        var values = new string[count];

        for (var i = 0; i < count; i++)
            values[i] = String();

        return values;
    }

    /// <summary>A list length, refused when it could not fit in what is left of the body.</summary>
    public int Count()
    {
        var count = Int();

        if (count < 0 || count > Remaining)
            throw new FormatException($"List count {count} does not fit in {Remaining} bytes.");

        return count;
    }

    private ReadOnlySpan<byte> Take(int length)
    {
        if (length > Remaining)
            throw new FormatException(
                $"Read of {length} bytes at {_position} runs past a {_body.Length}-byte body."
            );

        var span = _body.Span.Slice(_position, length);
        _position += length;

        return span;
    }
}
