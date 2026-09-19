using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

/// <summary>Plays or pauses the video everyone is watching on a display.</summary>
[GenerateSerializer, Immutable]
public sealed record YoutubeControlVideoMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId FurniId { get; init; }

    [Id(1)]
    public required YoutubeVideoStateType State { get; init; }
}
