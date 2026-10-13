using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>GetMarketplaceOffersMessageComposer</c>: a price range (-1 for none), a name, a sort type and whether limited editions are combined.</summary>
public record GetMarketplaceOffersMessage : IMessageEvent
{
    public required int MinPrice { get; init; }
    public required int MaxPrice { get; init; }
    public required string SearchQuery { get; init; }
    public required int SortType { get; init; }
    public required bool CombineUniques { get; init; }
}
