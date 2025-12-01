using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record CanCreateRoomEventMessageComposer : IComposer
{
    [Id(0)]
    public bool CanCreateEvent { get; init; }

    [Id(1)]
    public int ErrorCode { get; init; }
}
