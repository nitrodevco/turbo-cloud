using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record RoomEventMessageComposer : IComposer
{
    [Id(0)]
    public object? Data { get; init; }
}
