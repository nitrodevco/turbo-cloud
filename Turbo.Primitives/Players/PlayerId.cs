using Orleans;

namespace Turbo.Primitives.Players;

[GenerateSerializer, Immutable]
public readonly record struct PlayerId
{
    [Id(0)]
    public int Value { get; init; }

    public PlayerId(int value)
    {
        Value = value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator int(PlayerId id) => id.Value;

    public static implicit operator PlayerId(int value) => new(value);
}
