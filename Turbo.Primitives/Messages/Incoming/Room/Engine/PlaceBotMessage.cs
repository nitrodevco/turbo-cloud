using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Place a bot from the inventory; (0, 0) when no tile was chosen.</summary>
public record PlaceBotMessage : IMessageEvent
{
    public required int BotId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}
