using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Nft;

/// <summary>Wear the NFT outfit with this id (an id from <c>UserNftWardrobe</c>).</summary>
public record SaveUserNftWardrobeMessage : IMessageEvent
{
    public required string OutfitId { get; init; }
}
