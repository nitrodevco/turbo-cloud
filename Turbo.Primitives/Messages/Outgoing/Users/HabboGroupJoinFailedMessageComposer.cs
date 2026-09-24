using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// A join was refused. The client looks the reason up as <c>group.joinfail.&lt;n&gt;</c>, except
/// <see cref="GuildJoinFailedType.ClubRequired"/>, which opens its Habbo Club window instead.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboGroupJoinFailedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildJoinFailedType Reason { get; init; }
}
