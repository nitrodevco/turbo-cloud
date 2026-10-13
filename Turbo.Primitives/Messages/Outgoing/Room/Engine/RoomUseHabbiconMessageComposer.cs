using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>An avatar in the room shows a Habbicon; the client draws it over that avatar.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomUseHabbiconMessageComposer : IComposer
{
    /// <summary>The avatar's room object id.</summary>
    [Id(0)]
    public required int RoomIndex { get; init; }

    [Id(1)]
    public required int HabbiconId { get; init; }
}
