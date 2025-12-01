using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record OfficialRoomsMessageComposer : IComposer
{
    [Id(0)]
    public object? PromotedRooms { get; init; }

    [Id(1)]
    public object? Data { get; init; }

    [Id(2)]
    public object? AdRoom { get; init; }
}
