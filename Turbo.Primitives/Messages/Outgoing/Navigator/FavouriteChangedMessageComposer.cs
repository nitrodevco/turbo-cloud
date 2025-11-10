using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record FavouriteChangedMessageComposer : IComposer
{
    public int RoomId { get; init; }
    public bool Added { get; init; }
}
