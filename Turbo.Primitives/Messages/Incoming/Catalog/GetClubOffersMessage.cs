using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetClubOffersMessage : IMessageEvent
{
    public ClubOfferRequestSourceType RequestSource { get; init; }
}
