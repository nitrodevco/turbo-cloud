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
    public static WiredVariableHash HashFromValues(
        string variableName,
        WiredVariableTargetType targetType
    )
    {
        var hasher = new XxHash32();

        WriteString(ref hasher, variableName);
        WriteInt32(ref hasher, (int)targetType);

        Span<byte> hashBytes = stackalloc byte[4];
        hasher.GetHashAndReset(hashBytes);

        return new WiredVariableHash(BinaryPrimitives.ReadInt32LittleEndian(hashBytes));
    }

    public static WiredVariableHash HashVariableDefinition(IWiredVariableDefinition varDef)
    {
        var hasher = new XxHash32();

        WriteString(ref hasher, varDef.VariableName);
        WriteInt32(ref hasher, (int)varDef.AvailabilityType);
        WriteInt32(ref hasher, (int)varDef.TargetType);
        WriteInt32(ref hasher, (int)varDef.Flags);
        WriteInt32(ref hasher, varDef.TextConnectors.Count);

        foreach (var s in varDef.TextConnectors.Values)
            WriteString(ref hasher, s);

        Span<byte> hashBytes = stackalloc byte[4];
        hasher.GetHashAndReset(hashBytes);

        return new WiredVariableHash(BinaryPrimitives.ReadInt32LittleEndian(hashBytes));
    }

    public static WiredVariableHash HashFromHashes(IReadOnlyList<WiredVariableHash> hashes)
    {
        var hasher = new XxHash32();

        WriteInt32(ref hasher, hashes.Count);

        for (int i = 0; i < hashes.Count; i++)
            WriteInt32(ref hasher, hashes[i].Value);

        Span<byte> hashBytes = stackalloc byte[4];
        hasher.GetHashAndReset(hashBytes);

        return new WiredVariableHash(BinaryPrimitives.ReadInt32LittleEndian(hashBytes));
    }

    private static void WriteInt32(ref XxHash32 hasher, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        hasher.Append(buf);
    }

    private static void WriteString(ref XxHash32 hasher, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);

        WriteInt32(ref hasher, byteCount);

        Span<byte> tmp = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];

        Encoding.UTF8.GetBytes(value.AsSpan(), tmp);
        hasher.Append(tmp);
    }
}
