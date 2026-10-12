using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>
/// Recolorable furni that changed colour: the client writes each entry's channels into the map
/// data of every furni it names (Flash <c>RoomEngine.updateFurnitureColor</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurniRecolorUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RecolorUpdateSnapshot> Updates { get; init; }
}
