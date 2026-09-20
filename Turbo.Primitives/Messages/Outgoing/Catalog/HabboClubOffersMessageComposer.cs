using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record HabboClubOffersMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<ClubOfferSnapshot> Offers { get; init; }

    /// <summary>Echoed back from the request, so the client knows which window asked.</summary>
    [Id(1)]
    public required ClubOfferRequestSourceType RequestSource { get; init; }
}
