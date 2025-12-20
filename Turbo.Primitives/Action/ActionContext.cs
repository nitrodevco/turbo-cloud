using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Action;

[GenerateSerializer, Immutable]
public class ActionContext
{
    [Id(0)]
    public required ActionOrigin Origin { get; init; }

    [Id(1)]
    public SessionKey SessionKey { get; init; } = string.Empty;

    [Id(2)]
    public long PlayerId { get; init; } = -1;

    [Id(3)]
    public RoomId RoomId { get; init; } = -1;
}
