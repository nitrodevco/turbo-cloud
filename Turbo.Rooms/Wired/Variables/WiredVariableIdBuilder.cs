using System;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public static class WiredVariableIdBuilder
{
    public static WiredVariableId Create(WiredVariableIdSourceType sourceType, ulong payload56)
    {
        payload56 &= 0x00FF_FFFF_FFFF_FFFFUL;

        ulong value = ((ulong)sourceType << 56) | payload56;

        return new WiredVariableId(unchecked((long)value));
    }

    public static WiredVariableId CreateInternal(WiredVariableTargetType targetType, string name) =>
        Create(
            WiredVariableIdSourceType.Internal,
            HashInternalPayload56(WiredVariableIdSourceType.Internal, targetType, name)
        );

    public static WiredVariableId CreateDatabase(int itemId) =>
        Create(
            WiredVariableIdSourceType.Database,
            HashDatabasePayload56(WiredVariableIdSourceType.Database, itemId)
        );

    private static ulong HashInternalPayload56(
        WiredVariableIdSourceType sourceType,
        WiredVariableTargetType targetType,
        string name
    )
    {
        var hasher = new XxHash64();

        WriteByte(ref hasher, (byte)sourceType);
        WriteInt32(ref hasher, (int)targetType);
        WriteString(ref hasher, name);

        Span<byte> out8 = stackalloc byte[8];
        hasher.GetHashAndReset(out8);

        return BinaryPrimitives.ReadUInt64LittleEndian(out8) & 0x00FF_FFFF_FFFF_FFFFUL;
    }

    private static ulong HashDatabasePayload56(WiredVariableIdSourceType sourceType, int itemId)
    {
        var hasher = new XxHash64();

        WriteByte(ref hasher, (byte)sourceType);
        WriteInt32(ref hasher, itemId);

        Span<byte> out8 = stackalloc byte[8];
        hasher.GetHashAndReset(out8);

        return BinaryPrimitives.ReadUInt64LittleEndian(out8) & 0x00FF_FFFF_FFFF_FFFFUL;
    }

    private static void WriteByte(ref XxHash64 hasher, byte value)
    {
        Span<byte> b = [value];

        hasher.Append(b);
    }

    private static void WriteInt32(ref XxHash64 hasher, int value)
    {
        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buf, value);

        hasher.Append(buf);
    }

    private static void WriteString(ref XxHash64 hasher, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);

        WriteInt32(ref hasher, byteCount);

        Span<byte> tmp = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];
        Encoding.UTF8.GetBytes(value.AsSpan(), tmp);

        hasher.Append(tmp);
    }
}
