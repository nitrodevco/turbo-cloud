using System.Globalization;
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

    /// <summary>
    /// Invariant, because an altitude that is turned into text is going to a client or a
    /// database, and neither reads a decimal comma.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator double(Altitude id) => id.Value;

    public static implicit operator Altitude(double value) => new(value);

    public static Altitude Zero => new(0);

    public static Altitude FromValue(double value) => new(value);

    public static Altitude FromInt(int value) => new(value / 100.0);
}
