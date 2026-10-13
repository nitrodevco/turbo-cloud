using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Marketplace;

/// <summary>Flash <c>MakeOfferMessageComposer</c>: the price, the furni type (1 floor, 2 wall) and the refs of the items offered.</summary>
public record MakeOfferMessage : IMessageEvent
{
    public required int Price { get; init; }
    public required int FurniType { get; init; }
    public required ImmutableArray<int> ItemRefs { get; init; }
}
