using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

public static class WiredVariableIdBuilder
{
    public static WiredVariableId Create(WiredVariableIdSourceType sourceType, ulong payload48)
    {
        payload48 &= 0x0000_FFFF_FFFF_FFFFUL;

        var value = ((ulong)sourceType << 56) | payload48;

        return new WiredVariableId(value);
    }

    public static WiredVariableId CreateInternal(WiredVariableTargetType targetType, string name)
    {
        long h = WiredVariableHashBuilder.HashFromValues(name, targetType);
        ulong payload48 = (ulong)h & 0x0000_FFFF_FFFF_FFFFUL;

        return Create(WiredVariableIdSourceType.Internal, payload48);
    }

    public static WiredVariableId CreateDatabase(int itemId) =>
        Create(WiredVariableIdSourceType.Database, (ulong)itemId);
}
