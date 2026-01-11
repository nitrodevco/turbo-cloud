using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredValue(WiredValueType Kind, long I64, double F64, string? Str)
{
    public static WiredValue None() => new(WiredValueType.None, 0, 0, null);

    public static WiredValue Int(int v) => new(WiredValueType.Int, v, 0, null);

    public static WiredValue Bool(bool v) => new(WiredValueType.Bool, v ? 1 : 0, 0, null);

    public static WiredValue String(string v) => new(WiredValueType.String, 0, 0, v);

    public static WiredValue Double(double v) => new(WiredValueType.Double, 0, v, null);

    public static WiredValue Long(long v) => new(WiredValueType.Long, v, 0, null);

    public int AsInt() => (int)I64;

    public long AsLong() => I64;

    public bool AsBool() => I64 != 0;

    public double AsDouble() => Kind == WiredValueType.Double ? F64 : I64;

    public string AsString() => Str ?? string.Empty;
}
