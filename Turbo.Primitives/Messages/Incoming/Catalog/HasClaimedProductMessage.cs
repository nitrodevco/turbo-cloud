using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record HasClaimedProductMessage : IMessageEvent
{
    public required string ClaimId { get; init; }
}
