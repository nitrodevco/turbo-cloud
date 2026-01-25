using Orleans;

namespace Turbo.Primitives.Rooms.Object;

[GenerateSerializer, Immutable]
public readonly record struct Altitude
{
    [Id(0)]
    public double Value { get; init; }

    public Altitude(double value)
    {
        Value = value;
    }

    public int ToInt() => (int)(Value * 100);

    public override string ToString() => Value.ToString();

    public static implicit operator double(Altitude id) => id.Value;

    public static implicit operator Altitude(double value) => new(value);

    public static Altitude Zero => new(0);

    public static Altitude FromValue(double value) => new(value);

    public static Altitude FromInt(int value) => new(value / 100.0);
}
