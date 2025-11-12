using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record FavouriteChangedMessageComposer : IComposer
{
    [Id(0)]
    public int RoomId { get; init; }

    [Id(1)]
    public bool Added { get; init; }
}
