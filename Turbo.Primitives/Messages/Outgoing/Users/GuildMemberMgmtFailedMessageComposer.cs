using Orleans;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// Acting on a member failed, which in practice means another admin got there first. Looked up
/// as <c>group.membermgmt.fail.&lt;n&gt;</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildMemberMgmtFailedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildId GuildId { get; init; }

    [Id(1)]
    public required GuildMemberMgmtFailedType Reason { get; init; }
}
