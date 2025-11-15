using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Session;

[GenerateSerializer, Immutable]
public sealed record SessionKey
{
    [Id(0)]
    public required string Value { get; init; }

    public static SessionKey Empty => new() { Value = string.Empty };

    public static SessionKey From(string value) => new() { Value = value };
}
