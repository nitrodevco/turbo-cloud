using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableId(ulong Value)
{
    public long ToInt64() => unchecked((long)Value);

    public static explicit operator long(WiredVariableId id) => unchecked((long)id.Value);
}
