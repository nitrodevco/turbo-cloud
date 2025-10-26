using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record FavouritesMessage : IComposer
{
    public required int Limit { get; init; }
    public required List<int> FavoriteRoomIds { get; init; }
}
