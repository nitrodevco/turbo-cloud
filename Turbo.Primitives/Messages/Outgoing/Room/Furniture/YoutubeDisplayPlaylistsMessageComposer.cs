using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record YoutubeDisplayPlaylistsMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId FurniId { get; init; }

    [Id(1)]
    public required ImmutableArray<YoutubePlaylistSnapshot> Playlists { get; init; }

    /// <summary>Empty when none is selected.</summary>
    [Id(2)]
    public required string SelectedPlaylistId { get; init; }
}
