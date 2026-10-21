using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>One placed WIRED Link: the room it stands in and its furni id there.</summary>
[GenerateSerializer, Immutable]
public readonly record struct WiredLinkEndpoint(
    [property: Id(0)] RoomId RoomId,
    [property: Id(1)] int FurniId
);
