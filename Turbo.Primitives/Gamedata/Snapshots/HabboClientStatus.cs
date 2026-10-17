using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// Habbo's HTML client as the hotel has it: each environment, every release downloaded (newest
/// first), and the one the hotel builds from - the followed environment's - with why, when none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboClientStatus
{
    [Id(0)]
    public required ImmutableArray<HabboClientEnvironmentSnapshot> Environments { get; init; }

    [Id(1)]
    public required ImmutableArray<HabboClientReleaseSnapshot> Releases { get; init; }

    /// <summary>The environment followed: <c>production</c>, <c>sandbox</c>, or null when none has a release yet.</summary>
    [Id(2)]
    public string? Follow { get; init; }

    /// <summary>The release built from; null when there is none.</summary>
    [Id(3)]
    public string? Followed { get; init; }
}
