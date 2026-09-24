using System;
using System.Diagnostics.CodeAnalysis;
using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// A whole group, as its own grain holds it. Everything the summary does not carry because only
/// the group's own windows need it.
///
/// The member count is not here: it changes with every join and leave, and putting it on the
/// group's state would mean writing the group row for something the membership table already
/// knows. The grain counts members itself and sends the figure with the packets that show it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildSnapshot : GuildSummarySnapshot
{
    public GuildSnapshot() { }

    /// <summary>
    /// Starts from the summary, so the fields the two share are mapped once. The attribute is
    /// what lets the base fields count as set; it also stops the compiler from checking the
    /// fields below, so whoever calls this sets every one of them.
    /// </summary>
    [SetsRequiredMembers]
    public GuildSnapshot(GuildSummarySnapshot summary)
        : base(summary) { }

    [Id(0)]
    public required string Description { get; init; } = string.Empty;

    /// <summary>How far down the group's rights in its homeroom reach.</summary>
    [Id(1)]
    public required GuildRightsLevel RightsLevel { get; init; } = GuildRightsLevel.Admins;

    [Id(2)]
    public required DateTime CreatedAt { get; init; }
}
