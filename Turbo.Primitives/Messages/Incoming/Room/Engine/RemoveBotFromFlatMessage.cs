using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record RemoveBotFromFlatMessage : IMessageEvent
{
    public required int BotId { get; init; }
}
