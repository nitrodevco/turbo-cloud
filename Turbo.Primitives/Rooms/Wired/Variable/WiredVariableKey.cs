using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public readonly record struct WiredVariableKey(
    WiredVariableId VariableId,
    WiredVariableTargetType TargetType,
    int TargetId
)
{
    public string ToStorageKey() => $"{VariableId}|{(int)TargetType}|{TargetId}";

    public static WiredVariableKey FromStorageKey(string storageKey)
    {
        var parts = storageKey.Split('|');

        if (
            parts.Length != 3
            || !ulong.TryParse(parts[0], out var variableId)
            || !int.TryParse(parts[1], out var targetType)
            || !int.TryParse(parts[2], out var targetId)
        )
            throw new FormatException($"Invalid WiredVariableKey storage key: {storageKey}");

        return new WiredVariableKey(
            WiredVariableId.Parse(variableId.ToString()),
            (WiredVariableTargetType)targetType,
            targetId
        );
    }
}
