using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What putting Habbo's values back in some fields would change, before it is done.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboValuesPreview
{
    /// <summary>Definitions whose value differs from Habbo's, by furnidata key.</summary>
    [Id(0)]
    public required ImmutableDictionary<string, int> ByField { get; init; }

    /// <summary>Definitions with at least one such field.</summary>
    [Id(1)]
    public required int Definitions { get; init; }

    /// <summary>The release Habbo's values come from: the newest found. Null before Habbo was checked.</summary>
    [Id(2)]
    public HabboReleaseSnapshot? Release { get; init; }
}
