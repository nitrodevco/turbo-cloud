using System;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public static class WiredVariableIdBuilder
{
    private enum InternalPayloadKind : byte
    {
        Hashed = 0,
        Ordered = 1,
    }

    public enum WiredVarGroupBand : byte
    {
        Context = 0xF8, // (optional) put context very high if you want it topmost
        Furni = 0xF0,
        User = 0xE0,
        Global = 0xD0,
        Other = 0xC0,
    }

    public enum WiredVarSubBand : byte
    {
        Base = 0xE0,
        Position = 0xD0,
        Meta = 0xC0,
        Other = 0x80,
    }

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

    public static WiredVariableId CreateInternalOrdered(
        WiredVariableTargetType targetType,
        string name,
        WiredVarSubBand subBand,
        ushort order
    ) =>
        Create(
            WiredVariableIdSourceType.Internal,
            BuildOrderedInternalPayload56(
                targetType,
                name,
                MapGroupBand(targetType),
                subBand,
                order
            )
        );

    public static WiredVariableId CreateInternalOrdered(
        string name,
        WiredVarGroupBand groupBand,
        WiredVarSubBand subBand,
        ushort order
    ) =>
        Create(
            WiredVariableIdSourceType.Internal,
            BuildOrderedInternalPayload56(
                WiredVariableTargetType.None,
                name,
                groupBand,
                subBand,
                order
            )
        );

    public static WiredVariableId CreateDatabase(int itemId) =>
        Create(
            WiredVariableIdSourceType.Database,
            HashDatabasePayload56(WiredVariableIdSourceType.Database, itemId)
        );

    private static WiredVarGroupBand MapGroupBand(WiredVariableTargetType targetType) =>
        targetType switch
        {
            WiredVariableTargetType.Furni => WiredVarGroupBand.Furni,
            WiredVariableTargetType.User => WiredVarGroupBand.User,
            WiredVariableTargetType.Global => WiredVarGroupBand.Global,
            WiredVariableTargetType.Context => WiredVarGroupBand.Context,
            _ => WiredVarGroupBand.Other,
        };

    private static ulong BuildOrderedInternalPayload56(
        WiredVariableTargetType targetTypeForTieHash,
        string name,
        WiredVarGroupBand groupBand,
        WiredVarSubBand subBand,
        ushort order
    )
    {
        ulong kind = (ulong)InternalPayloadKind.Ordered;
        ulong group = (byte)groupBand;
        ulong sub = (byte)subBand;
        ulong ord = order;

        ushort tie16 = Hash16ForOrdered(targetTypeForTieHash, groupBand, subBand, order, name);

        ulong payload = (kind << 48) | (group << 40) | (sub << 32) | (ord << 16) | tie16;

        return payload & 0x00FF_FFFF_FFFF_FFFFUL;
    }

    private static ushort Hash16ForOrdered(
        WiredVariableTargetType targetType,
        WiredVarGroupBand groupBand,
        WiredVarSubBand subBand,
        ushort order,
        string name
    )
    {
        var hasher = new XxHash64();

        WriteByte(ref hasher, (byte)InternalPayloadKind.Ordered);

        WriteInt32(ref hasher, (int)targetType);

        WriteByte(ref hasher, (byte)groupBand);
        WriteByte(ref hasher, (byte)subBand);
        WriteInt32(ref hasher, order);
        WriteString(ref hasher, name);

        Span<byte> out8 = stackalloc byte[8];
        hasher.GetHashAndReset(out8);

        ulong h64 = BinaryPrimitives.ReadUInt64LittleEndian(out8);
        return (ushort)(h64 & 0xFFFF);
    }

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
