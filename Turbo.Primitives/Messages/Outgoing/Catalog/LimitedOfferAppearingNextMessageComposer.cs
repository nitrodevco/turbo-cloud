using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record LimitedOfferAppearingNextMessageComposer : IComposer
{
    [Id(0)]
    public required int AppearsInSeconds { get; init; }

    [Id(1)]
    public required int PageId { get; init; }

    [Id(2)]
    public required int OfferId { get; init; }

    [Id(3)]
    public required string ProductClassName { get; init; }
}
