using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// The catalog page the reception's expiring page widget counts down to; an empty name hides the
/// widget.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CatalogPageWithEarliestExpiryMessageComposer : IComposer
{
    [Id(0)]
    public required string PageName { get; init; }

    [Id(1)]
    public required int SecondsToExpiry { get; init; }

    [Id(2)]
    public required string Image { get; init; }
}
