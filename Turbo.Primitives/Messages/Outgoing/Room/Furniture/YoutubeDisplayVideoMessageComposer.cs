using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

/// <summary>What a video display shows now, and how far into it the room already is.</summary>
[GenerateSerializer, Immutable]
public sealed record YoutubeDisplayVideoMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId FurniId { get; init; }

    /// <summary>Empty when the display has nothing to play.</summary>
    [Id(1)]
    public required string VideoId { get; init; }

    [Id(2)]
    public required int StartAtSeconds { get; init; }

    [Id(3)]
    public required int EndAtSeconds { get; init; }

    [Id(4)]
    public required YoutubeVideoStateType State { get; init; }
}
