using System;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Session;

[GenerateSerializer, Immutable]
public readonly record struct SessionKey
{
    [Id(0)]
    public string Value { private get; init; }

    public SessionKey(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value;

    public static implicit operator string(SessionKey key) => key.Value;

    public static implicit operator SessionKey(string value) => new(value);
}
