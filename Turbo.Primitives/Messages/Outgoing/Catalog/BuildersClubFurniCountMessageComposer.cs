using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// How many furni this player has borrowed from the Builders Club, across every room. The client
/// starts at -1 and refuses to place anything until it has been told once, so this is answered
/// whenever it asks and sent again after each borrow and return.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BuildersClubFurniCountMessageComposer : IComposer
{
    [Id(0)]
    public required int FurniCount { get; init; }
}
