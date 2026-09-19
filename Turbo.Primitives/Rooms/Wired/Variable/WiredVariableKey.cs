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

    /// <summary>Reads a key back from storage. Stored text is not trusted: a key that does not parse is refused, not thrown.</summary>
    public static bool TryFromStorageKey(string storageKey, out WiredVariableKey key)
    {
        key = default;

        var parts = storageKey.Split('|');

        if (
            parts.Length != 3
            || !WiredVariableId.TryParse(parts[0], out var variableId)
            || !int.TryParse(parts[1], out var targetType)
            || !Enum.IsDefined((WiredVariableTargetType)targetType)
            || !int.TryParse(parts[2], out var targetId)
        )
            return false;

        key = new WiredVariableKey(variableId, (WiredVariableTargetType)targetType, targetId);

        return true;
    }
}
