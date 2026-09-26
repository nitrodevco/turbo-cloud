using Orleans;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Primitives.Guilds.Snapshots;

/// <summary>
/// What came of acting on a member. A refusal the client has a text for carries
/// <see cref="Failure"/>; one it does not — the actor simply had no right to try — carries
/// nothing, because there is nothing to tell them that is not already obvious from a window
/// they should not have had open.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMemberMgmtResultSnapshot
{
    [Id(0)]
    public required bool Succeeded { get; init; }

    [Id(1)]
    public GuildMemberMgmtFailedType? Failure { get; init; }

    /// <summary>
    /// Set instead of <see cref="Failure"/> when the refusal is about the target's own ability
    /// to be in another group rather than about the group acting. The hotel has its own two
    /// texts for it (<c>group.joinfail.5</c> and <c>.6</c>), reached through
    /// <c>HabboGroupJoinFailed</c>, so it travels as a join failure and not as a management one.
    /// </summary>
    [Id(2)]
    public GuildJoinFailedType? JoinFailure { get; init; }

    public static GuildMemberMgmtResultSnapshot Success() => new() { Succeeded = true };

    public static GuildMemberMgmtResultSnapshot Refused() => new() { Succeeded = false };

    public static GuildMemberMgmtResultSnapshot Failed(GuildMemberMgmtFailedType failure) =>
        new() { Succeeded = false, Failure = failure };

    public static GuildMemberMgmtResultSnapshot FailedToJoin(GuildJoinFailedType failure) =>
        new() { Succeeded = false, JoinFailure = failure };
}
