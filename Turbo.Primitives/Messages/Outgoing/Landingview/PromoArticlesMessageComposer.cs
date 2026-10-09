using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Landingview;

/// <summary>The reception's promo articles, in the order the carousel shows them.</summary>
[GenerateSerializer, Immutable]
public sealed record PromoArticlesMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<PromoArticleSnapshot> Articles { get; init; }
}
