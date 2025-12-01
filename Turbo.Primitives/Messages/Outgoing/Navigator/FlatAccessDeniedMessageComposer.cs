using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record FlatAccessDeniedMessageComposer : IComposer
{
    [Id(0)]
    public int RoomId { get; init; }

    [Id(1)]
    public string? Username { get; init; }
}
