using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public static class WiredVariableHashBuilder
{
    public static long HashFromValues(string variableName, WiredVariableTargetType targetType)
    {
        var hasher = new XxHash64();

        WriteString(ref hasher, variableName);
        WriteInt32(ref hasher, (int)targetType);

        Span<byte> hashBytes = stackalloc byte[8];
        hasher.GetHashAndReset(hashBytes);

        return BinaryPrimitives.ReadInt64LittleEndian(hashBytes);
    }

    public static long HashVariableDefinition(IWiredVariableDefinition varDef)
    {
        var hasher = new XxHash64();

        WriteString(ref hasher, varDef.VariableName);
        WriteInt32(ref hasher, (int)varDef.AvailabilityType);
        WriteInt32(ref hasher, (int)varDef.TargetType);
        WriteInt32(ref hasher, (int)varDef.Flags);
        WriteInt32(ref hasher, varDef.TextConnectors.Count);

        foreach (var s in varDef.TextConnectors.Values)
            WriteString(ref hasher, s);

        Span<byte> hashBytes = stackalloc byte[8];
        hasher.GetHashAndReset(hashBytes);

        return BinaryPrimitives.ReadInt64LittleEndian(hashBytes);
    }

    public static long HashFromHashes(IReadOnlyList<long> hashes)
    {
        var hasher = new XxHash64();

        WriteInt32(ref hasher, hashes.Count);

        for (int i = 0; i < hashes.Count; i++)
            WriteInt64(ref hasher, hashes[i]);

        Span<byte> out8 = stackalloc byte[8];
        hasher.GetHashAndReset(out8);

        return BinaryPrimitives.ReadInt64LittleEndian(out8);
    }

    private static void WriteInt32(ref XxHash64 hasher, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);

        hasher.Append(buf);
    }

    private static void WriteInt64(ref XxHash64 hasher, long value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, value);
        hasher.Append(b);
    }

    private static void WriteString(ref XxHash64 hasher, string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);

        WriteInt32(ref hasher, byteCount);

        Span<byte> tmp = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];
        Encoding.UTF8.GetBytes(value.AsSpan(), tmp);

        hasher.Append(tmp);
    }
}
