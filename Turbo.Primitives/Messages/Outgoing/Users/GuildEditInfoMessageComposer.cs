using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// The manage-group window. It carries the group as it is now plus the rooms that could have
/// been its homeroom — the client lists them even though the choice can no longer be changed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildEditInfoMessageComposer : IComposer
{
    [Id(0)]
    public required GuildSnapshot Guild { get; init; }

    [Id(1)]
    public required ImmutableArray<GuildRoomOptionSnapshot> OwnedRooms { get; init; }

    [Id(2)]
    public required bool IsOwner { get; init; }

    /// <summary>The current badge, read back out of the code so the editor reopens on it.</summary>
    [Id(3)]
    public required ImmutableArray<GuildBadgePartSnapshot> BadgeParts { get; init; }

    [Id(4)]
    public required int MemberCount { get; init; }
}
