using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Furni;

/// <summary>
/// Several items left the furni list at once; the client removes each and resets the furni
/// tab's new marks once (Flash <c>IncomingMessages.onFurniListRemoveMultiple</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurniListRemoveMultipleEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomObjectId> ItemIds { get; init; }
}
