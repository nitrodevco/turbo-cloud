using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetClubOffersMessage : IMessageEvent
{
    public ClubOfferRequestSourceEnum RequestSource { get; init; }
}
