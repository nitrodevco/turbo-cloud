using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record FavouritesMessage : IComposer
{
    public required int Limit { get; init; }
    public required List<int> FavoriteRoomIds { get; init; }
}
