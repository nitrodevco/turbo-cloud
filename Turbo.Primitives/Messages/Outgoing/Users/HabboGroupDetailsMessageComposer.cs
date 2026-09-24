using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// The group details window. <see cref="View"/> is the group grain's; the three fields beside
/// it come from the room, the player directory and the viewer's own guild grain, read side by
/// side by the handler because the group grain must not call them.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HabboGroupDetailsMessageComposer : IComposer
{
    [Id(0)]
    public required GuildViewSnapshot View { get; init; }

    /// <summary>The homeroom's name.</summary>
    [Id(1)]
    public required string RoomName { get; init; }

    [Id(2)]
    public required string OwnerName { get; init; }

    /// <summary>Whether this is the group whose badge the viewer wears.</summary>
    [Id(3)]
    public required bool IsFavourite { get; init; }

    /// <summary>Echoed from the request: whether to open the window or refresh it in place.</summary>
    [Id(4)]
    public required bool OpenDetails { get; init; }
}
