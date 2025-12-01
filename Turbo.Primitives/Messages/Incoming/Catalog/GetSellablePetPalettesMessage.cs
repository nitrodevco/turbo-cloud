using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetSellablePetPalettesMessage : IMessageEvent
{
    public int LocalizationId { get; init; }
}
