using System;
using System.Globalization;
using Orleans;

namespace Turbo.Primitives.Guilds;

[GenerateSerializer, Immutable]
public readonly record struct GuildId(int Value) : IComparable<GuildId>
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(GuildId other) => Value.CompareTo(other.Value);

    public static GuildId Parse(int value) => new(value);

    public static implicit operator int(GuildId id) => id.Value;

    public static implicit operator GuildId(int value) => new(value);

    public static GuildId Invalid => new(-1);
}
