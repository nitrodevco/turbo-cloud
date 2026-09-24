using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// One page of a group's roster, as the member window asks for it. The window draws its own
/// paging from <see cref="TotalEntries"/> and <see cref="PageSize"/>, and echoes the search back
/// so it can tell a stale answer from a fresh one — which is why both travel back out.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMemberPageSnapshot
{
    [Id(0)]
    public required GuildSummarySnapshot Guild { get; init; }

    /// <summary>Everyone the search matched, not just this page.</summary>
    [Id(1)]
    public required int TotalEntries { get; init; }

    [Id(2)]
    public required ImmutableArray<GuildMemberSnapshot> Members { get; init; }

    /// <summary>
    /// Whether the viewer may act on these rows. The window hides its kick and approve buttons
    /// on this, and drops the pending and blocked filters from its dropdown.
    /// </summary>
    [Id(3)]
    public required bool AllowedToManage { get; init; }

    [Id(4)]
    public required int PageSize { get; init; }

    [Id(5)]
    public required int PageIndex { get; init; }

    [Id(6)]
    public required GuildMemberSearchType SearchType { get; init; }

    [Id(7)]
    public required string NameFilter { get; init; }
}
