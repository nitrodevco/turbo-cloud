using System;
using System.Globalization;
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
            || !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var targetType
            )
            || !Enum.IsDefined((WiredVariableTargetType)targetType)
            || !int.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var targetId
            )
        )
            return false;

        key = new WiredVariableKey(variableId, (WiredVariableTargetType)targetType, targetId);

        return true;
    }
}
