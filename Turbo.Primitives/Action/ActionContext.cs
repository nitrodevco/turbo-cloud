using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Action;

[GenerateSerializer, Immutable]
public readonly record struct ActionContext
{
    [Id(0)]
    public required ActionOrigin Origin { get; init; }

    [Id(1)]
    public required SessionKey SessionKey { get; init; }

    [Id(2)]
    public required PlayerId PlayerId { get; init; }

    [Id(3)]
    public required RoomId RoomId { get; init; }
}
