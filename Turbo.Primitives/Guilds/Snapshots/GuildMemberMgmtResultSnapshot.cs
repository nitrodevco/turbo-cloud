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

    public static GuildMemberMgmtResultSnapshot Success() => new() { Succeeded = true };

    public static GuildMemberMgmtResultSnapshot Refused() => new() { Succeeded = false };

    public static GuildMemberMgmtResultSnapshot Failed(GuildMemberMgmtFailedType failure) =>
        new() { Succeeded = false, Failure = failure };
}
