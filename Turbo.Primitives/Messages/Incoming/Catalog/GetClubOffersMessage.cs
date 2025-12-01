using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetClubOffersMessage : IMessageEvent
{
    public ClubOfferRequestSourceType RequestSource { get; init; }
}
