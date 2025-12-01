using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record ChargeFireworkMessage : IMessageEvent
{
    public int SpriteId { get; init; }
    public int Type { get; init; }
}
