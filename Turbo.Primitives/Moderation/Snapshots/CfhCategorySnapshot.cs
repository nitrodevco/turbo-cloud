using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>A group of call for help topics; the client titles it <c>help.cfh.reason.&lt;name&gt;</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record CfhCategorySnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required ImmutableArray<CfhTopicSnapshot> Topics { get; init; }
}
