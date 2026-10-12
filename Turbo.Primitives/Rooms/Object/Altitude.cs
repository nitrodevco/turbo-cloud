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

    /// <summary>
    /// The height as an avatar status carries it (<c>mv 5,5,1.0</c>, <c>sit 1.0</c>): always with a
    /// decimal, as Habbo writes it (recorded on hhus, 2026-10-12).
    /// </summary>
    public string ToStatusString() =>
        Value.ToString("0.0###############", CultureInfo.InvariantCulture);

    public static implicit operator double(Altitude id) => id.Value;

    public static implicit operator Altitude(double value) => new(value);

    public static Altitude Zero => new(0);

    public static Altitude FromValue(double value) => new(value);

    public static Altitude FromInt(int value) => new(value / 100.0);
}
