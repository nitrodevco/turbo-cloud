using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Reads a logged packet's payload as Habbo's client does: integers in four bytes, big end
/// first, a boolean in one, a string as its UTF-8 bytes after their count in two. Reading past
/// the end, or a count more than what is left could hold, throws
/// <see cref="InvalidDataException"/>: the packet is not laid out as it is read.
/// </summary>
internal sealed class PacketPayloadReader(byte[] payload)
{
    private readonly byte[] _payload = payload;
    private int _at;

    public int Remaining => _payload.Length - _at;

    public int ReadInt() => BinaryPrimitives.ReadInt32BigEndian(Take(sizeof(int)));

    public bool ReadBoolean() => Take(1)[0] != 0;

    public string ReadString()
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(Take(sizeof(ushort)));

        return Encoding.UTF8.GetString(Take(length));
    }

    /// <summary>How many of something follow, each at least <paramref name="leastBytes"/> long.</summary>
    public int ReadCount(int leastBytes)
    {
        var count = ReadInt();

        if (count < 0 || (long)count * leastBytes > Remaining)
            throw new InvalidDataException(
                $"a list of {count} at byte {_at - sizeof(int)} is more than the {Remaining} bytes left"
            );

        return count;
    }

    /// <summary>Throws unless every byte has been read.</summary>
    public void End()
    {
        if (Remaining != 0)
            throw new InvalidDataException($"{Remaining} bytes are left over after reading it");
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
            throw new InvalidDataException(
                $"it ends at byte {_payload.Length}, before the {count} bytes needed at {_at}"
            );

        var span = _payload.AsSpan(_at, count);
        _at += count;

        return span;
    }
}
