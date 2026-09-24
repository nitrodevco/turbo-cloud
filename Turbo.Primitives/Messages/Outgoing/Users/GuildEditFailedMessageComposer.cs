using Orleans;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// A create or edit was refused. The client looks the reason up as
/// <c>group.edit.fail.&lt;n&gt;</c>, except <see cref="GuildEditFailedType.ClubRequired"/>, which
/// opens its Habbo Club window instead.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildEditFailedMessageComposer : IComposer
{
    [Id(0)]
    public required GuildEditFailedType Reason { get; init; }
}
