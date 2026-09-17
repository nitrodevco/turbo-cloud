using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record GuestRoomSearchResultMessageComposer : IComposer
{
    [Id(0)]
    public required NavigatorSearchType SearchType { get; init; }

    [Id(1)]
    public required string SearchParam { get; init; }

    [Id(2)]
    public required ImmutableArray<RoomInfoSnapshot> Rooms { get; init; }
}
