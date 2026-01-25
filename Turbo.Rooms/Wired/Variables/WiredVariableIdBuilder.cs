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
        Context = 0xF8,
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

    public static WiredVariableId CreateFromBoxId(int boxId) =>
        new(
            ((ulong)((int)WiredVariableIdSourceType.Database & 0b1_1111) << 48) | HashBoxId48(boxId)
        );

    public static WiredVariableId CreateInternalOrdered(
        WiredVariableTargetType targetType,
        string name,
        WiredVarSubBand subBand,
        ushort order
    ) => new(CreateOrdered(targetType, name, MapGroupBand(targetType), subBand, order));

    private static WiredVarGroupBand MapGroupBand(WiredVariableTargetType targetType) =>
        targetType switch
        {
            WiredVariableTargetType.Furni => WiredVarGroupBand.Furni,
            WiredVariableTargetType.User => WiredVarGroupBand.User,
            WiredVariableTargetType.Global => WiredVarGroupBand.Global,
            WiredVariableTargetType.Context => WiredVarGroupBand.Context,
            _ => WiredVarGroupBand.Other,
        };

    public static ulong CreateOrdered(
        WiredVariableTargetType targetType,
        string name,
        WiredVarGroupBand groupBand,
        WiredVarSubBand subBand,
        ushort order
    )
    {
        ushort tie16 = HashTie16(targetType, groupBand, subBand, order, name);

        ulong id =
            ((ulong)(byte)groupBand << 40)
            | ((ulong)(byte)subBand << 32)
            | ((ulong)order << 16)
            | tie16;

        return id;
    }

    private static ushort HashTie16(
        WiredVariableTargetType targetType,
        WiredVarGroupBand groupBand,
        WiredVarSubBand subBand,
        ushort order,
        string name
    )
    {
        var hasher = new XxHash64();

        WriteInt32BE(ref hasher, (int)targetType);
        WriteByte(ref hasher, (byte)groupBand);
        WriteByte(ref hasher, (byte)subBand);
        WriteInt32BE(ref hasher, order);
        WriteString(ref hasher, name);

        return (ushort)(hasher.GetCurrentHashAsUInt64() & 0xFFFF);
    }

    private static ulong HashBoxId48(int boxId)
    {
        var hasher = new XxHash64();

        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buf, boxId);

        hasher.Append(buf);

        return hasher.GetCurrentHashAsUInt64() & 0x0000_FFFF_FFFF_FFFFUL;
    }

    private static void WriteByte(ref XxHash64 hasher, byte value)
    {
        Span<byte> b = [value];

        hasher.Append(b);
    }

    private static void WriteInt32BE(ref XxHash64 hasher, int value)
    {
        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buf, value);

        hasher.Append(buf);
    }

    private static void WriteString(ref XxHash64 hasher, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);

        WriteInt32BE(ref hasher, byteCount);

        Span<byte> tmp = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

        Encoding.UTF8.GetBytes(value.AsSpan(), tmp);

        hasher.Append(tmp);
    }
}
