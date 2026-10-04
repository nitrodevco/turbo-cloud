using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Turbo.LoadBots.Protocol;

/// <summary>
/// Builds the payload of one client message the way the Flash client does: big-endian ints and
/// shorts, one-byte booleans, strings as an unsigned short byte length and UTF-8.
/// </summary>
public sealed class PacketWriter
{
    private readonly List<byte> _bytes = [];

    public PacketWriter Int(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _bytes.AddRange(buffer);

        return this;
    }

    public PacketWriter Short(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _bytes.AddRange(buffer);

        return this;
    }

    public PacketWriter Bool(bool value)
    {
        _bytes.Add(value ? (byte)1 : (byte)0);

        return this;
    }

    public PacketWriter String(string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);

        if (encoded.Length > ushort.MaxValue)
            throw new ArgumentException(
                "A protocol string holds at most 65535 bytes.",
                nameof(value)
            );

        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)encoded.Length);
        _bytes.AddRange(length);
        _bytes.AddRange(encoded);

        return this;
    }

    public PacketWriter Ints(IReadOnlyCollection<int> values)
    {
        Int(values.Count);

        foreach (var value in values)
            Int(value);

        return this;
    }

    public PacketWriter Strings(IReadOnlyCollection<string> values)
    {
        Int(values.Count);

        foreach (var value in values)
            String(value);

        return this;
    }

    public byte[] ToArray() => [.. _bytes];
}
